using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Calendar;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data.Stores;

/// <summary>
/// Server-side <see cref="ICalendarStore"/>, scoped to the current account.
///
/// Series are stored; occurrences are computed by <see cref="RecurrenceExpander"/>
/// on read. Only the requested window bounds that work, which is why every range
/// query takes an explicit from and to.
/// </summary>
public sealed class SqlCalendarStore(AdaDbContext db, IAccountContext account, TimeProvider clock) : ICalendarStore
{
    /// <summary>
    /// How far back a range query looks for series that could still be producing
    /// occurrences inside the window. A yearly rule needs at least a year of
    /// slack; two years leaves room for "every other year".
    /// </summary>
    private static readonly TimeSpan SeriesLookback = TimeSpan.FromDays(366 * 2);

    public async Task<CalendarEventDto> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);

        var recurrence = request.Recurrence ?? RecurrenceDto.None;

        var entity = new CalendarEventEntity
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.AccountId,
            Title = request.Title.Trim(),
            Location = Blank(request.Location),
            Notes = Blank(request.Notes),
            StartsUtc = request.StartsUtc,
            EndsUtc = request.EndsUtc ?? request.StartsUtc.AddHours(1),
            IsAllDay = request.IsAllDay,
            ReminderEnabled = request.ReminderEnabled,
            ReminderMinutesBefore = Math.Clamp(request.ReminderMinutesBefore, 0, 60 * 24 * 7),
            Frequency = recurrence.Frequency,
            Interval = recurrence.Interval > 0 ? recurrence.Interval : 1,
            RecurrenceUntil = recurrence.Until,
            RecurrenceCount = recurrence.Count,
            TimeZoneId = NormaliseZone(request.TimeZoneId),
            CreatedUtc = clock.GetUtcNow(),
        };

        db.CalendarEvents.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<CalendarEventDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.CalendarEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.AccountId == account.AccountId, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> ListSeriesAsync(int limit, CancellationToken cancellationToken)
    {
        var entities = await db.CalendarEvents
            .AsNoTracking()
            .Where(e => e.AccountId == account.AccountId)
            .OrderByDescending(e => e.StartsUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDto)];
    }

    public async Task<IReadOnlyList<EventOccurrenceDto>> ListOccurrencesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (toUtc <= fromUtc)
        {
            return [];
        }

        // Candidates are anything starting before the window closes. A one-off is
        // filtered tightly by its end; a series needs the lookback, because it may
        // have started long ago and still be repeating into view.
        var earliest = fromUtc - SeriesLookback;

        var candidates = await db.CalendarEvents
            .AsNoTracking()
            .Include(e => e.Exceptions)
            .Where(e => e.AccountId == account.AccountId
                && e.StartsUtc < toUtc
                && (e.Frequency != RecurrenceFrequency.None || e.EndsUtc > fromUtc)
                && (e.Frequency == RecurrenceFrequency.None || e.StartsUtc > earliest))
            .ToListAsync(cancellationToken);

        var occurrences = new List<EventOccurrenceDto>();

        foreach (var series in candidates)
        {
            var cancelled = series.Exceptions.Select(x => x.OccurrenceStartUtc).ToHashSet();

            var expanded = RecurrenceExpander.Expand(
                series.StartsUtc,
                series.EndsUtc,
                ToRecurrence(series),
                RecurrenceExpander.ResolveZone(series.TimeZoneId),
                fromUtc,
                toUtc);

            foreach (var (start, end) in expanded)
            {
                if (cancelled.Contains(start))
                {
                    continue;
                }

                occurrences.Add(new EventOccurrenceDto(
                    series.Id,
                    series.Title,
                    series.Location,
                    start,
                    end,
                    series.IsAllDay,
                    series.ReminderEnabled,
                    series.ReminderMinutesBefore,
                    series.Frequency != RecurrenceFrequency.None,
                    series.TimeZoneId));
            }
        }

        return [.. occurrences.OrderBy(o => o.StartsUtc)];
    }

    public async Task<CalendarEventDto?> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);

        var entity = await db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Id == id && e.AccountId == account.AccountId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var recurrence = request.Recurrence ?? RecurrenceDto.None;

        entity.Title = request.Title.Trim();
        entity.Location = Blank(request.Location);
        entity.Notes = Blank(request.Notes);
        entity.StartsUtc = request.StartsUtc;
        entity.EndsUtc = request.EndsUtc ?? request.StartsUtc.AddHours(1);
        entity.IsAllDay = request.IsAllDay;
        entity.ReminderEnabled = request.ReminderEnabled;
        entity.ReminderMinutesBefore = Math.Clamp(request.ReminderMinutesBefore, 0, 60 * 24 * 7);
        entity.Frequency = recurrence.Frequency;
        entity.Interval = recurrence.Interval > 0 ? recurrence.Interval : 1;
        entity.RecurrenceUntil = recurrence.Until;
        entity.RecurrenceCount = recurrence.Count;
        entity.TimeZoneId = NormaliseZone(request.TimeZoneId);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<bool> CancelOccurrenceAsync(
        Guid eventId,
        DateTimeOffset occurrenceStartUtc,
        CancellationToken cancellationToken)
    {
        // Ownership is checked before anything is written, so an exception row can
        // never be attached to someone else's series.
        var owned = await db.CalendarEvents
            .AnyAsync(e => e.Id == eventId && e.AccountId == account.AccountId, cancellationToken);

        if (!owned)
        {
            return false;
        }

        var already = await db.CalendarExceptions
            .AnyAsync(x => x.EventId == eventId && x.OccurrenceStartUtc == occurrenceStartUtc, cancellationToken);

        if (already)
        {
            return true;
        }

        db.CalendarExceptions.Add(new CalendarExceptionEntity
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId,
            OccurrenceStartUtc = occurrenceStartUtc,
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        // Exceptions cascade from the series.
        var deleted = await db.CalendarEvents
            .Where(e => e.Id == id && e.AccountId == account.AccountId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Rejects a zone id this machine cannot resolve rather than storing it.
    /// Keeping an unresolvable id would silently expand every future occurrence
    /// in UTC while the row still claimed otherwise.
    /// </summary>
    private static string? NormaliseZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        var trimmed = timeZoneId.Trim();
        return RecurrenceExpander.ResolveZone(trimmed) == TimeZoneInfo.Utc && trimmed != TimeZoneInfo.Utc.Id
            ? null
            : trimmed;
    }

    private static RecurrenceDto ToRecurrence(CalendarEventEntity e) =>
        new(e.Frequency, e.Interval, e.RecurrenceUntil, e.RecurrenceCount);

    private static CalendarEventDto ToDto(CalendarEventEntity e) =>
        new(e.Id, e.Title, e.Location, e.Notes, e.StartsUtc, e.EndsUtc, e.IsAllDay,
            e.ReminderEnabled, e.ReminderMinutesBefore, ToRecurrence(e), e.TimeZoneId, e.CreatedUtc);
}
