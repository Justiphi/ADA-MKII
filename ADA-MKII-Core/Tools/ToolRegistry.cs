using System.Text.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Core.Logging;
using Microsoft.Extensions.Logging;

namespace ADA_MKII_Core.Tools;

/// <summary>
/// Dispatches to whichever <see cref="IAssistantTool"/> implementations were
/// registered. Tools are matched case-insensitively, because models are not
/// consistent about casing.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IAssistantTool> _tools;
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(IEnumerable<IAssistantTool> tools, ILogger<ToolRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(tools);

        _logger = logger;
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        Definitions = [.. _tools.Values.Select(t => new LlmToolDefinition(t.Name, t.Description, t.JsonSchema))];
    }

    public IReadOnlyList<LlmToolDefinition> Definitions { get; }

    public async Task<ToolResult> InvokeAsync(string name, string argumentsJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || !_tools.TryGetValue(name, out var tool))
        {
            return ToolResult.Failure($"There is no tool called '{name}'.");
        }

        JsonElement arguments;

        try
        {
            // An absent or empty argument blob is a no-argument call, not an error.
            using var document = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);

            arguments = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            // Models do emit malformed JSON. Telling it so is cheaper than
            // failing the turn, and it usually gets it right on the retry.
            return ToolResult.Failure($"Those arguments were not valid JSON: {ex.Message}");
        }

        // Deliberately not logged with its arguments: a tool call carries the
        // same user content as a message, and message bodies are not logged.
        CoreLog.ToolInvoked(_logger, tool.Name);

        return await tool.InvokeAsync(arguments, cancellationToken);
    }
}
