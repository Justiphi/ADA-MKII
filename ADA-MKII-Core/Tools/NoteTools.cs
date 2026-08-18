using System.Globalization;
using System.Text.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Tools;

/// <summary>Writes a note down for the user.</summary>
public sealed class CreateNoteTool(INoteStore notes) : IAssistantTool
{
    public string Name => "create_note";

    public string Description =>
        "Write a note down for the user. Use this when they ask you to note, jot, "
        + "write down or remember something they will want to read back later, such as "
        + "a shopping list, an idea, or instructions. For facts about the user "
        + "themselves that should influence future conversations, use remember instead.";

    public string JsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "content": { "type": "string", "description": "The body of the note." },
            "title": { "type": "string", "description": "Optional short title. Omit to derive one from the content." }
          },
          "required": ["content"]
        }
        """;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var content = ToolArgs.String(arguments, "content");

        if (string.IsNullOrWhiteSpace(content))
        {
            return ToolResult.Failure("A note needs content.");
        }

        var note = await notes.CreateAsync(
            new CreateNoteRequest(ToolArgs.String(arguments, "title"), content),
            cancellationToken);

        return ToolResult.Success($"Saved the note \"{note.Title}\".");
    }
}

/// <summary>Reads notes back, so ADA can answer questions about them.</summary>
public sealed class SearchNotesTool(INoteStore notes) : IAssistantTool
{
    private const int MaxResults = 10;

    public string Name => "search_notes";

    public string Description =>
        "Search the user's notes, or list the most recent ones when no query is given. "
        + "Use this before answering any question about what they wrote down.";

    public string JsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "Words to look for. Omit to list recent notes." }
          }
        }
        """;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var query = ToolArgs.String(arguments, "query");

        var found = string.IsNullOrWhiteSpace(query)
            ? await notes.ListAsync(MaxResults, cancellationToken)
            : await notes.SearchAsync(query, MaxResults, cancellationToken);

        if (found.Count == 0)
        {
            return ToolResult.Success("No notes matched.");
        }

        var lines = found.Select(n => string.Create(
            CultureInfo.InvariantCulture,
            $"- {n.Title}: {n.Content}"));

        return ToolResult.Success(string.Join('\n', lines));
    }
}
