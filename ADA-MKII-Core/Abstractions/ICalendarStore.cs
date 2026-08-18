using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Calendar persistence. Account-scoped like every other store.
///
/// Note the shape: series are stored, occurrences are computed. Asking for a date
/// range returns expanded occurrences; asking by id returns the series.
/// </summary>
public interface ICalendarStore
{
    Task<CalendarEventDto> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken);

    Task<CalendarEventDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Every stored series, newest first. For a management list, not a calendar view.</summary>
    Task<IReadOnlyList<CalendarEventDto>> ListSeriesAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Occurrences that fall inside the window, ordered by start, with recurring
    /// series expanded and cancelled occurrences removed.
    /// </summary>
    Task<IReadOnlyList<EventOccurrenceDto>> ListOccurrencesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<CalendarEventDto?> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a single occurrence of a series without touching the rest - "skip
    /// just this week". Returns false if the series does not exist.
    /// </summary>
    Task<bool> CancelOccurrenceAsync(Guid eventId, DateTimeOffset occurrenceStartUtc, CancellationToken cancellationToken);

    /// <summary>Deletes the whole series, including any occurrence exceptions.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
