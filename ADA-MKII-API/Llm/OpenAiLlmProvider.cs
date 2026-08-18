using System.ClientModel;
using System.Runtime.CompilerServices;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace ADA_MKII_API.Llm;

/// <summary>
/// Streaming completions over the OpenAI wire format. Lives here rather than in
/// Core because this is the only place allowed to know which vendor is behind
/// <see cref="ILlmProvider"/>.
///
/// The same protocol is spoken by Ollama, Groq, OpenRouter and others, so
/// pointing <see cref="OpenAiOptions.BaseUrl"/> elsewhere is all that is needed
/// to leave OpenAI entirely.
/// </summary>
public sealed class OpenAiLlmProvider(IOptions<OpenAiOptions> options) : ILlmProvider
{
    private readonly OpenAiOptions _options = options.Value;

    public string Name => "openai";

    public async IAsyncEnumerable<LlmDelta> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = CreateClient(request.Model, request.Endpoint);

        var messages = request.Messages.Select(ToChatMessage).ToList();
        var chatOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxOutputTokens,
            Temperature = (float)request.Temperature,
        };

        var stream = client.CompleteChatStreamingAsync(messages, chatOptions, cancellationToken);

        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return new LlmDelta(part.Text);
                }
            }

            // Usage arrives on the final update, and only when the service
            // chooses to report it - callers must tolerate it being absent.
            // A local backend often omits it, which quietly starves the spend
            // guard; that is acceptable because a local backend costs nothing.
            if (update.Usage is { } usage)
            {
                yield return new LlmDelta(
                    null,
                    new LlmUsage(usage.InputTokenCount, usage.OutputTokenCount));
            }
        }
    }

    private ChatClient CreateClient(string model, Uri? requested)
    {
        var configured = ParseConfiguredBaseUrl();

        // An operator running accounts for other people can switch this off; the
        // check belongs here rather than in the UI, because the UI is not what
        // makes the outbound request.
        if (!_options.AllowUserBaseUrl)
        {
            requested = null;
        }

        var endpoint = requested ?? configured;

        // The API key goes ONLY to the endpoint the operator configured. A user
        // who points ADA at their own address gets an unauthenticated client,
        // because the alternative is handing this server's OpenAI key to any
        // host a signed-in user cares to name.
        var isConfiguredEndpoint = requested is null || (configured is not null && SameHost(requested, configured));
        var key = isConfiguredEndpoint ? _options.ApiKey : null;

        if (endpoint is null)
        {
            // Default OpenAI, which is useless without a key.
            var credential = new ApiKeyCredential(_options.ApiKey
                ?? throw new InvalidOperationException(
                    "Ada:OpenAI:ApiKey is not configured, and no endpoint was selected. Set the key, "
                    + "or point Ada:OpenAI:BaseUrl at an OpenAI-compatible server."));

            return new ChatClient(model, credential);
        }

        // Local backends such as Ollama ignore the credential, but the client
        // requires one, so send a placeholder rather than nothing.
        return new ChatClient(
            model,
            new ApiKeyCredential(key ?? "not-required"),
            new OpenAIClientOptions { Endpoint = endpoint });
    }

    private Uri? ParseConfiguredBaseUrl() =>
        Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var parsed) ? parsed : null;

    /// <summary>Scheme, host and port - the parts that decide where bytes actually go.</summary>
    private static bool SameHost(Uri left, Uri right) =>
        Uri.Compare(left, right, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;

    private static ChatMessage ToChatMessage(LlmMessage message) => message.Role switch
    {
        ChatRole.System => new SystemChatMessage(message.Content),
        ChatRole.Assistant => new AssistantChatMessage(message.Content),
        _ => new UserChatMessage(message.Content),
    };
}
