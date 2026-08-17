using System.Runtime.CompilerServices;
using System.Text.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Client;

/// <summary>
/// Client-side <see cref="IAssistantPipeline"/>: POSTs a turn and re-materialises
/// the server-sent event stream as <see cref="ChatStreamEvent"/>s.
///
/// Implementing the same interface as the server-side pipeline is what lets the
/// shared UI consume one abstraction and remain oblivious to whether the
/// orchestration is in-process or across the network.
/// </summary>
public sealed class HttpAssistantPipeline(HttpClient http, ISessionStore session)
    : AuthenticatedHttpClient(http, session), IAssistantPipeline
{
    private const string DataPrefix = "data:";

    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Web;

    public async IAsyncEnumerable<ChatStreamEvent> RunAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // ResponseHeadersRead is essential: the default buffers the whole
        // response, which would turn a stream into a single delayed lump.
        using var response = await SendAsync(
            HttpMethod.Post,
            ApiRoutes.Chat,
            request,
            cancellationToken,
            HttpCompletionOption.ResponseHeadersRead);

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // Loop on the async read rather than EndOfStream, which blocks the thread
        // synchronously and would stall on a stream that is open but idle.
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            // Blank lines separate events; anything not a data field is metadata
            // this client has no use for.
            if (!line.StartsWith(DataPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line[DataPrefix.Length..].Trim();
            if (payload.Length == 0)
            {
                continue;
            }

            var evt = JsonSerializer.Deserialize<ChatStreamEvent>(payload, SerializerOptions);
            if (evt is not null)
            {
                yield return evt;
            }
        }
    }
}
