using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Data.Entities;

/// <summary>
/// A calendar series. One row however many times it repeats - occurrences are
/// expanded on read, never stored, because an open-ended series has no end to
/// write down.
/// </summary>
public sealed class CalendarEventEntity
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public AccountEntity? Account { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Location { get; set; }

    public string? Notes { get; set; }

    /// <summary>Start of the first occurrence. Later ones are derived from it.</summary>
    public DateTimeOffset StartsUtc { get; set; }

    public DateTimeOffset EndsUtc { get; set; }

    public bool IsAllDay { get; set; }

    public bool ReminderEnabled { get; set; }

    public int ReminderMinutesBefore { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>Every N periods. 1 for "every week", 2 for "every other week".</summary>
    public int Interval { get; set; } = 1;

    /// <summary>Stop repeating after this instant. Null with no count means open-ended.</summary>
    public DateTimeOffset? RecurrenceUntil { get; set; }

    /// <summary>Stop after this many occurrences. Null means unlimited.</summary>
    public int? RecurrenceCount { get; set; }

    /// <summary>
    /// The IANA or Windows zone the series was scheduled in, e.g.
    /// "Pacific/Auckland". Null means UTC.
    ///
    /// A <see cref="DateTimeOffset"/> alone is not enough for a repeating event:
    /// it records the offset that applied on the first occurrence, not the rule
    /// that produced it, so a 09:00 meeting slides to 08:00 the moment its zone
    /// leaves daylight saving. The zone is what keeps the wall-clock time fixed.
    /// </summary>
    public string? TimeZoneId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public ICollection<CalendarExceptionEntity> Exceptions { get; } = [];
}

/// <summary>
/// A single occurrence removed from its series - "skip just this week". Stored
/// rather than baked into the rule so the series stays one row.
/// </summary>
public sealed class CalendarExceptionEntity
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public CalendarEventEntity? Event { get; set; }

    /// <summary>The start instant of the occurrence being cancelled.</summary>
    public DateTimeOffset OccurrenceStartUtc { get; set; }
}
