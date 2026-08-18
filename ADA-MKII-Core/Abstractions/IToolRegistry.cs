using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Discovery and dispatch for <see cref="IAssistantTool"/>.
///
/// The pipeline talks to this rather than to a list of tools, so what ADA can do
/// is a registration concern: a head that composes no tools simply has a model
/// that never calls one.
/// </summary>
public interface IToolRegistry
{
    /// <summary>Tools to advertise to the model. Empty disables tool calling for the turn.</summary>
    IReadOnlyList<LlmToolDefinition> Definitions { get; }

    /// <summary>
    /// Runs a tool by name. An unknown name is a <see cref="ToolResult.Failure"/>
    /// rather than an exception - models do invent tool names, and that should
    /// cost one wasted iteration, not the whole turn.
    /// </summary>
    Task<ToolResult> InvokeAsync(string name, string argumentsJson, CancellationToken cancellationToken);
}
