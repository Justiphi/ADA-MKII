namespace ADA_MKII_Core.Contracts;

/// <summary>How often a series repeats.</summary>
public enum RecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4,
}

/// <summary>
/// A repeat rule. Deliberately not RFC 5545: this covers what a personal
/// calendar actually needs, and an RRULE parser is a large amount of subtle code
/// to get wrong. See CLAUDE.md if external calendar sync is ever revisited.
/// </summary>
public sealed record RecurrenceDto(
    RecurrenceFrequency Frequency,
    int Interval,
    DateTimeOffset? Until,
    int? Count)
{
    public static RecurrenceDto None { get; } = new(RecurrenceFrequency.None, 1, null, null);

    public bool Repeats => Frequency != RecurrenceFrequency.None;
}

/// <summary>
/// A calendar event. When it repeats, this is the series definition - the
/// individual occurrences are produced by <see cref="EventOccurrenceDto"/> and
/// never stored, because "every weekday, forever" has no end to store.
/// </summary>
public sealed record CalendarEventDto(
    Guid Id,
    string Title,
    string? Location,
    string? Notes,
    DateTimeOffset StartsUtc,
    DateTimeOffset EndsUtc,
    bool IsAllDay,
    bool ReminderEnabled,
    int ReminderMinutesBefore,
    RecurrenceDto Recurrence,
    string? TimeZoneId,
    DateTimeOffset CreatedUtc);

/// <summary>
/// One dated instance of an event, produced by expanding a series over a window.
/// <see cref="EventId"/> points back at the series it came from.
/// </summary>
public sealed record EventOccurrenceDto(
    Guid EventId,
    string Title,
    string? Location,
    DateTimeOffset StartsUtc,
    DateTimeOffset EndsUtc,
    bool IsAllDay,
    bool ReminderEnabled,
    int ReminderMinutesBefore,
    bool IsRecurring,
    string? TimeZoneId);

public sealed record CreateEventRequest(
    string Title,
    DateTimeOffset StartsUtc,
    DateTimeOffset? EndsUtc,
    string? Location = null,
    string? Notes = null,
    bool IsAllDay = false,
    bool ReminderEnabled = true,
    int ReminderMinutesBefore = 15,
    RecurrenceDto? Recurrence = null,
    string? TimeZoneId = null);

public sealed record UpdateEventRequest(
    string Title,
    DateTimeOffset StartsUtc,
    DateTimeOffset? EndsUtc,
    string? Location = null,
    string? Notes = null,
    bool IsAllDay = false,
    bool ReminderEnabled = true,
    int ReminderMinutesBefore = 15,
    RecurrenceDto? Recurrence = null,
    string? TimeZoneId = null);

/// <summary>Skip one occurrence of a series, identified by its start instant.</summary>
public sealed record CancelOccurrenceRequest(DateTimeOffset OccurrenceStartUtc);
