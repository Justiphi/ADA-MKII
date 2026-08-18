namespace ADA_MKII_Core.Contracts;

/// <summary>
/// A tool call the model asked for.
///
/// <see cref="ArgumentsJson"/> is raw JSON rather than a parsed shape because
/// only the tool knows what its own arguments look like, and a model is quite
/// capable of sending something that does not match its schema - which the tool
/// must handle rather than the transport.
/// </summary>
public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>
/// A tool as advertised to the model: name, prose description, and a JSON Schema
/// for the arguments.
/// </summary>
public sealed record LlmToolDefinition(string Name, string Description, string JsonSchema);

/// <summary>One message as handed to a model provider. Deliberately not the same
/// type as <see cref="ChatMessageDto"/>: persistence and prompting have different
/// concerns and should be free to diverge.</summary>
public sealed record LlmMessage(ChatRole Role, string Content)
{
    /// <summary>
    /// Tool calls this assistant message is asking for. Set only on
    /// <see cref="ChatRole.Assistant"/>, and only inside a tool loop - the
    /// provider has to replay it so the model can see its own request.
    /// </summary>
    public IReadOnlyList<LlmToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// Which call this message answers. Set only on <see cref="ChatRole.Tool"/>.
    /// The id has to match, or the model cannot pair a result with its request.
    /// </summary>
    public string? ToolCallId { get; init; }
}

/// <summary>Token accounting for a single turn. Persisted so spend is measurable.</summary>
public sealed record LlmUsage(int TokensIn, int TokensOut);

/// <summary>A request to a model provider.</summary>
public sealed record LlmRequest(
    string Model,
    IReadOnlyList<LlmMessage> Messages,
    int MaxOutputTokens,
    double Temperature)
{
    /// <summary>
    /// Where to send it. Null means the provider's configured default. Set when
    /// the user points ADA at an OpenAI-compatible server of their own.
    /// </summary>
    public Uri? Endpoint { get; init; }

    /// <summary>
    /// Tools the model may call. Empty means none are offered.
    ///
    /// A backend that does not support tool calling simply never asks for one,
    /// and the turn is an ordinary reply - which is why nothing above this needs
    /// to know whether the backend can do it.
    /// </summary>
    public IReadOnlyList<LlmToolDefinition> Tools { get; init; } = [];
}

/// <summary>
/// One chunk of a streamed completion. <see cref="Text"/> carries content;
/// <see cref="Usage"/> is populated on the final chunk, if the provider reports
/// it; <see cref="ToolCalls"/> arrives once, complete, when the model has
/// finished asking for tools.
/// </summary>
public sealed record LlmDelta(
    string? Text,
    LlmUsage? Usage = null,
    IReadOnlyList<LlmToolCall>? ToolCalls = null);
