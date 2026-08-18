using ADA_MKII_Core.Calendar;

namespace ADA_MKII_Core.Pipeline;

/// <summary>
/// Where and when the person asking actually is, for the duration of one turn.
///
/// Scoped, and set by the pipeline at the start of the turn. Tools read it
/// rather than taking it as an argument, which keeps
/// <see cref="Abstractions.IAssistantTool.InvokeAsync"/> to the two parameters
/// it should have and stops every tool schema from carrying a redundant
/// timezone field the model would have to fill in correctly.
///
/// Zone matters here for the same reason it does in the calendar: "tomorrow at
/// 3" is meaningless without it, and the server's own zone is not the user's.
/// </summary>
public sealed class AssistantTurnContext(TimeProvider clock)
{
    /// <summary>The user's zone. UTC until the turn sets otherwise.</summary>
    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>Now, as an absolute instant.</summary>
    public DateTimeOffset Now => clock.GetUtcNow();

    /// <summary>Now, as the wall-clock time the user would read off a wall.</summary>
    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(Now, Zone);

    public void SetZone(string? timeZoneId) => Zone = RecurrenceExpander.ResolveZone(timeZoneId);

    /// <summary>
    /// Turns a wall-clock time the model produced into an absolute instant, by
    /// attaching the offset the user's zone had on that date. A model is told the
    /// local time and asked for local times back; it is never asked to do zone
    /// arithmetic itself, because it is bad at it.
    /// </summary>
    public DateTimeOffset ToInstant(DateTime wallClock)
    {
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Zone.GetUtcOffset(unspecified));
    }
}
