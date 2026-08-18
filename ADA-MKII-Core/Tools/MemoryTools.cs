using System.Text.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Tools;

/// <summary>
/// Keeps a fact about the user across conversations.
///
/// The description is doing the real work here: left vaguer, a model will either
/// remember nothing or remember every passing remark, and the second failure is
/// worse - this is the most sensitive text in the system and the user sees all
/// of it on the memories page.
/// </summary>
public sealed class RememberTool(IMemoryStore memories) : IAssistantTool
{
    public string Name => "remember";

    public string Description =>
        "Store a durable fact about the user so future conversations can use it. "
        + "Good candidates: their preferences, relationships, where they work, ongoing "
        + "projects, constraints like allergies. Do not store passing chit-chat, anything "
        + "they asked you to do right now, or things that will be false next week. "
        + "Prefer one short, self-contained sentence. Check recall first so you do not "
        + "store something twice.";

    public string JsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "content": { "type": "string", "description": "One self-contained sentence, in the third person." },
            "tag": { "type": "string", "description": "Optional single-word category, e.g. preference, work, family." }
          },
          "required": ["content"]
        }
        """;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var content = ToolArgs.String(arguments, "content");

        if (string.IsNullOrWhiteSpace(content))
        {
            return ToolResult.Failure("A memory needs content.");
        }

        await memories.CreateAsync(
            new CreateMemoryRequest(content, ToolArgs.String(arguments, "tag")),
            cancellationToken);

        return ToolResult.Success("Noted, and I will remember it.");
    }
}

/// <summary>Looks up what ADA already knows about the user.</summary>
public sealed class RecallTool(IMemoryStore memories) : IAssistantTool
{
    private const int MaxResults = 10;

    public string Name => "recall";

    public string Description =>
        "Look up what you already know about the user. Use this when a question turns on "
        + "something personal you were not told in this conversation, and before storing "
        + "a new memory that may duplicate one.";

    public string JsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "Words to look for. Omit for the most recent memories." }
          }
        }
        """;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var query = ToolArgs.String(arguments, "query");

        var found = string.IsNullOrWhiteSpace(query)
            ? await memories.ListRecentAsync(MaxResults, cancellationToken)
            : await memories.SearchAsync(query, MaxResults, cancellationToken);

        return found.Count == 0
            ? ToolResult.Success("Nothing remembered about that.")
            : ToolResult.Success(string.Join('\n', found.Select(m => $"- {m.Content}")));
    }
}
