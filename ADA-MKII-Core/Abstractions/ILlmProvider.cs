using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// A streaming language-model backend. Implemented by OpenAiLlmProvider in
/// ADA-MKII-API; swapping providers is a single registration change because
/// nothing above this interface knows which one is in use.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Provider name, for logging and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Streams a completion. Must honour <paramref name="cancellationToken"/>
    /// promptly: an abandoned request that keeps streaming is money on fire.
    /// </summary>
    IAsyncEnumerable<LlmDelta> StreamAsync(LlmRequest request, CancellationToken cancellationToken);
}
