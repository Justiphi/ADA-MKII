namespace ADA_MKII_Core.Contracts;

/// <summary>One message as handed to a model provider. Deliberately not the same
/// type as <see cref="ChatMessageDto"/>: persistence and prompting have different
/// concerns and should be free to diverge.</summary>
public sealed record LlmMessage(ChatRole Role, string Content);

/// <summary>Token accounting for a single turn. Persisted so spend is measurable.</summary>
public sealed record LlmUsage(int TokensIn, int TokensOut);

/// <summary>A request to a model provider.</summary>
public sealed record LlmRequest(
    string Model,
    IReadOnlyList<LlmMessage> Messages,
    int MaxOutputTokens,
    double Temperature);

/// <summary>
/// One chunk of a streamed completion. <see cref="Text"/> carries content;
/// <see cref="Usage"/> is populated on the final chunk, if the provider reports it.
/// </summary>
public sealed record LlmDelta(string? Text, LlmUsage? Usage = null);
