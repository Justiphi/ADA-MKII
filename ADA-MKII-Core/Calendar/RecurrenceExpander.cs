using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Calendar;

/// <summary>
/// Turns a series definition into the occurrences that fall inside a window.
///
/// Lives in Core, and is pure, because this is the part of a calendar most likely
/// to be subtly wrong - and the part worth testing without a database.
///
/// **Why a time zone and not just the offset.** Stepping a
/// <see cref="DateTimeOffset"/> forward preserves its offset, which is wrong the
/// moment daylight saving changes: a 09:00 meeting in a +13 zone silently becomes
/// 08:00 once that zone moves to +12. An offset is not a zone and cannot know the
/// rule. So occurrences are stepped in the series' own wall-clock time and the
/// offset is recomputed from the zone at each new date.
/// </summary>
public static class RecurrenceExpander
{
    /// <summary>
    /// A hard ceiling on how many occurrences one series may contribute. A corrupt
    /// rule must not be able to produce an unbounded list.
    /// </summary>
    public const int MaxOccurrencesPerSeries = 1000;

    /// <summary>
    /// Expands a series over [fromUtc, toUtc). An occurrence is included when it
    /// overlaps the window at all, so an event already in progress still shows.
    /// </summary>
    /// <param name="zone">
    /// The zone the series was scheduled in. UTC is a safe default for a one-off,
    /// but a repeating event without its real zone will drift across DST.
    /// </param>
    public static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> Expand(
        DateTimeOffset seriesStart,
        DateTimeOffset seriesEnd,
        RecurrenceDto? recurrence,
        TimeZoneInfo zone,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var duration = seriesEnd - seriesStart;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (recurrence is null || !recurrence.Repeats)
        {
            if (Overlaps(seriesStart, seriesStart + duration, fromUtc, toUtc))
            {
                yield return (seriesStart, seriesStart + duration);
            }

            yield break;
        }

        // An interval of zero would never advance; treat it as the sane default
        // rather than looping forever on bad data.
        var interval = recurrence.Interval > 0 ? recurrence.Interval : 1;

        // The wall-clock time the series happens at, in its own zone. Every
        // occurrence keeps this time of day.
        var wallStart = TimeZoneInfo.ConvertTime(seriesStart, zone).DateTime;

        var produced = 0;

        for (var step = 0; step < MaxOccurrencesPerSeries; step++)
        {
            var wall = Advance(wallStart, recurrence.Frequency, interval * step);
            var start = Resolve(wall, zone);
            var end = start + duration;

            // Count-limited series count every occurrence, not just the visible
            // ones - "the next 10" means 10 from the series start.
            produced++;

            if (recurrence.Count is { } count && produced > count)
            {
                yield break;
            }

            if (recurrence.Until is { } until && start > until)
            {
                yield break;
            }

            // Past the window: nothing later can qualify either, since starts only
            // increase.
            if (start >= toUtc)
            {
                yield break;
            }

            if (Overlaps(start, end, fromUtc, toUtc))
            {
                yield return (start, end);
            }
        }
    }

    /// <summary>
    /// Attaches the offset the zone actually had at that wall-clock moment.
    ///
    /// Spring-forward creates wall times that never happen. `GetUtcOffset` resolves
    /// those to the pre-transition offset rather than throwing, which pushes the
    /// occurrence to the nearest real instant - the behaviour a person expects from
    /// "my 2:30am alarm" on the morning 2:30am does not exist.
    /// </summary>
    private static DateTimeOffset Resolve(DateTime wall, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    private static DateTime Advance(DateTime from, RecurrenceFrequency frequency, int amount) =>
        frequency switch
        {
            RecurrenceFrequency.Daily => from.AddDays(amount),
            RecurrenceFrequency.Weekly => from.AddDays(7 * amount),
            RecurrenceFrequency.Monthly => from.AddMonths(amount),
            RecurrenceFrequency.Yearly => from.AddYears(amount),
            _ => from,
        };

    private static bool Overlaps(DateTimeOffset start, DateTimeOffset end, DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        start < toUtc && end > fromUtc;

    /// <summary>
    /// Resolves a stored zone id, falling back to UTC. Ids are user-supplied, and a
    /// machine that does not know the zone should not take the calendar down with it.
    /// </summary>
    public static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
