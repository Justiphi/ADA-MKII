using System.Runtime.CompilerServices;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Pipeline;

/// <summary>
/// A deterministic stand-in for a real model. Selected automatically when no
/// provider API key is configured, which keeps the whole pipeline - streaming,
/// persistence, cost accounting, tool calling - runnable and testable without
/// spending money or requiring a credential. Never reaches the network.
/// </summary>
public sealed class EchoLlmProvider : ILlmProvider
{
    /// <summary>
    /// Types a tool call by hand: <c>!tool create_note {"content":"milk"}</c>.
    ///
    /// This exists because the tool loop is the one part of the pipeline that
    /// cannot be exercised without a model willing to call a tool, and requiring
    /// a paid credential to test the loop at all would mean it never got tested.
    /// </summary>
    public const string ToolMarker = "!tool ";

    public string Name => "echo";

    public async IAsyncEnumerable<LlmDelta> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var last = request.Messages.LastOrDefault(m => m.Role is ChatRole.User or ChatRole.Tool);

        // Answering a tool result ends the loop, exactly as a real model would:
        // it has what it asked for and now has something to say.
        if (last?.Role == ChatRole.Tool)
        {
            await foreach (var delta in StreamWordsAsync($"[echo] The tool said: {last.Content}", request, cancellationToken))
            {
                yield return delta;
            }

            yield break;
        }

        var prompt = last?.Content ?? string.Empty;

        if (TryParseToolCall(prompt, request.Tools, out var call, out var problem))
        {
            yield return new LlmDelta(null, ToolCalls: [call]);
            yield break;
        }

        var reply = problem ?? $"[echo provider] You said: {prompt}";

        await foreach (var delta in StreamWordsAsync(reply, request, cancellationToken))
        {
            yield return delta;
        }
    }

    private static async IAsyncEnumerable<LlmDelta> StreamWordsAsync(
        string reply,
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// Reads <c>!tool name {json}</c>. Returns false with a
    /// <paramref name="problem"/> to say aloud when the marker was used but the
    /// tool is not on offer - silence would look like the loop was broken.
    /// </summary>
    private static bool TryParseToolCall(
        string prompt,
        IReadOnlyList<LlmToolDefinition> available,
        out LlmToolCall call,
        out string? problem)
    {
        call = null!;
        problem = null;

        var trimmed = prompt.TrimStart();

        if (!trimmed.StartsWith(ToolMarker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = trimmed[ToolMarker.Length..].TrimStart();
        var split = rest.IndexOf(' ', StringComparison.Ordinal);

        var name = split < 0 ? rest : rest[..split];
        var argumentsJson = split < 0 ? "{}" : rest[(split + 1)..].Trim();

        if (name.Length == 0)
        {
            problem = "[echo] Usage: !tool <name> {\"arg\":\"value\"}";
            return false;
        }

        if (!available.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            var names = available.Count == 0 ? "none" : string.Join(", ", available.Select(t => t.Name));
            problem = $"[echo] No tool called '{name}' is on offer. Available: {names}";
            return false;
        }

        call = new LlmToolCall($"echo-{Guid.CreateVersion7():N}", name, argumentsJson);
        return true;
    }
}
