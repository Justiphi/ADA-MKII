using System.Text.Json;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// What a tool hands back to the model.
///
/// <see cref="Ok"/> is separate from <see cref="Content"/> because a failure is
/// still a result the model must see: "that date did not parse" lets it try
/// again, whereas an exception would end the turn. Tools throw only for genuine
/// faults, never for bad arguments.
/// </summary>
public sealed record ToolResult(bool Ok, string Content)
{
    public static ToolResult Success(string content) => new(true, content);

    public static ToolResult Failure(string reason) => new(false, reason);
}

/// <summary>
/// Something ADA can do rather than just talk about.
///
/// Implementations depend on the store abstractions, never on a DbContext: a
/// tool is a thin adapter between a JSON argument blob and a Core abstraction.
/// </summary>
public interface IAssistantTool
{
    /// <summary>
    /// The name the model calls. Must match <c>^[a-zA-Z0-9_-]{1,64}$</c>, which
    /// is what the OpenAI wire format accepts.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Prose telling the model when to reach for this. This is the entire
    /// mechanism by which a tool gets used correctly, so it is worth as much
    /// care as the code.
    /// </summary>
    string Description { get; }

    /// <summary>JSON Schema for the arguments object.</summary>
    string JsonSchema { get; }

    /// <summary>
    /// Runs the tool. <paramref name="arguments"/> is whatever the model sent and
    /// is not guaranteed to match <see cref="JsonSchema"/>; return a
    /// <see cref="ToolResult.Failure"/> rather than throwing when it does not.
    /// </summary>
    Task<ToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken);
}
