using System.Globalization;
using System.Text.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Core.Pipeline;

namespace ADA_MKII_Core.Tools;

/// <summary>Puts something in the calendar, optionally repeating.</summary>
public sealed class CreateEventTool(ICalendarStore calendar, AssistantTurnContext turn) : IAssistantTool
{
    private const int DefaultDurationMinutes = 60;

    public string Name => "create_event";

    public string Description =>
        "Add an event or reminder to the user's calendar. Use this whenever they ask to be "
        + "reminded of something at a time, or to schedule, book or diarise anything. "
        + "Give starts_at as the user's own local time - never convert it to UTC.";

    public string JsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "What the event is." },
            "starts_at": {
              "type": "string",
              "description": "Local start time in ISO-8601 without a timezone, e.g. 2026-08-20T15:00."
            },
            "duration_minutes": { "type": "integer", "description": "Defaults to 60." },
            "location": { "type": "string" },
            "repeats": {
              "type": "string",
              "enum": ["none", "daily", "weekly", "monthly", "yearly"],
              "description": "Defaults to none."
            },
            "repeat_count": { "type": "integer", "description": "Stop after this many occurrences. Omit for open-ended." },
            "reminder_minutes_before": { "type": "integer", "description": "Defaults to 15." }
          },
          "required": ["title", "starts_at"]
        }
        """;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var title = ToolArgs.String(arguments, "title");

        if (string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Failure("An event needs a title.");
        }

        if (ToolArgs.WallClock(arguments, "starts_at") is not { } wall)
        {
            return ToolResult.Failure(
                "starts_at was missing or unparseable. Use ISO-8601 local time, e.g. 2026-08-20T15:00.");
        }

        var starts = turn.ToInstant(wall);
        var minutes = Math.Clamp(ToolArgs.Int(arguments, "duration_minutes") ?? DefaultDurationMinutes, 0, 1440);

        if (ParseFrequency(ToolArgs.String(arguments, "repeats")) is not { } frequency)
        {
            return ToolResult.Failure("repeats must be one of: none, daily, weekly, monthly, yearly.");
        }

        var recurrence = frequency == RecurrenceFrequency.None
            ? null
            : new RecurrenceDto(frequency, 1, null, ToolArgs.Int(arguments, "repeat_count"));

        var created = await calendar.CreateAsync(
            new CreateEventRequest(
                title,
                starts,
                starts.AddMinutes(minutes),
                ToolArgs.String(arguments, "location"),
                null,
                false,
                true,
                Math.Clamp(ToolArgs.Int(arguments, "reminder_minutes_before") ?? 15, 0, 60 * 24 * 7),
                recurrence,
                // The zone, not the offset - so a repeating event keeps its
                // wall-clock time when daylight saving moves.
                turn.Zone.Id),
            cancellationToken);

        var when = TimeZoneInfo.ConvertTime(created.StartsUtc, turn.Zone);

        return ToolResult.Success(string.Create(
            CultureInfo.InvariantCulture,
            $"Created \"{created.Title}\" on {when:dddd d MMMM} at {when:HH:mm}{(recurrence is null ? "" : $", repeating {frequency.ToString().ToLowerInvariant()}")}."));
    }

    private static RecurrenceFrequency? ParseFrequency(string? raw) => raw?.ToLowerInvariant() switch
    {
        null or "" or "none" => RecurrenceFrequency.None,
        "daily" => RecurrenceFrequency.Daily,
        "weekly" => RecurrenceFrequency.Weekly,
        "monthly" => RecurrenceFrequency.Monthly,
        "yearly" => RecurrenceFrequency.Yearly,
        _ => null,
    };
}

/// <summary>Reads the calendar back over a window.</summary>
public sealed class ListEventsTool(ICalendarStore calendar, AssistantTurnContext turn) : IAssistantTool
{
    private const int DefaultDays = 7;
    private const int MaxDays = 90;

    public string Name => "list_events";

    public string Description =>
        "List what is in the user's calendar over the coming days, with repeating events "
        + "already expanded. Use this before answering any question about their schedule, "
        + "what they have on, or whether they are free.";

    public string JsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "days": { "type": "integer", "description": "How many days ahead to look. Defaults to 7, maximum 90." },
            "starting": {
              "type": "string",
              "description": "Local date to start from in ISO-8601, e.g. 2026-08-20. Defaults to today."
            }
          }
        }
        """;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var days = Math.Clamp(ToolArgs.Int(arguments, "days") ?? DefaultDays, 1, MaxDays);

        var startWall = ToolArgs.WallClock(arguments, "starting")?.Date
            ?? turn.LocalNow.Date;

        var from = turn.ToInstant(startWall);
        var occurrences = await calendar.ListOccurrencesAsync(from, from.AddDays(days), cancellationToken);

        if (occurrences.Count == 0)
        {
            return ToolResult.Success($"Nothing scheduled in the next {days} days.");
        }

        var lines = occurrences.Select(o =>
        {
            var local = TimeZoneInfo.ConvertTime(o.StartsUtc, turn.Zone);

            return string.Create(
                CultureInfo.InvariantCulture,
                $"- {local:ddd d MMM} {local:HH:mm} {o.Title}{(string.IsNullOrWhiteSpace(o.Location) ? "" : $" at {o.Location}")}");
        });

        return ToolResult.Success(string.Join('\n', lines));
    }
}
