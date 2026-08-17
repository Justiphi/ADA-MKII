using System.Runtime.CompilerServices;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Pipeline;

/// <summary>
/// A deterministic stand-in for a real model. Selected automatically when no
/// provider API key is configured, which keeps the whole pipeline - streaming,
/// persistence, cost accounting - runnable and testable without spending money
/// or requiring a credential. Never reaches the network.
/// </summary>
public sealed class EchoLlmProvider : ILlmProvider
{
    public string Name => "echo";

    public async IAsyncEnumerable<LlmDelta> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = request.Messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? string.Empty;
        var reply = $"[echo provider] You said: {prompt}";

        var words = reply.Split(' ');
        foreach (var word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A small delay so streaming behaviour is observable rather than
            // arriving as one indivisible chunk.
            await Task.Delay(15, cancellationToken);
            yield return new LlmDelta(word + " ");
        }

        // Rough stand-in for real accounting: whitespace-delimited words.
        var tokensIn = request.Messages.Sum(m => m.Content.Split(' ').Length);
        yield return new LlmDelta(null, new LlmUsage(tokensIn, words.Length));
    }
}
