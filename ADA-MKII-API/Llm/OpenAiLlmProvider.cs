using System.ClientModel;
using System.Runtime.CompilerServices;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace ADA_MKII_API.Llm;

/// <summary>
/// Streaming completions from OpenAI. Lives here rather than in Core because
/// this is the only place allowed to know which vendor is behind
/// <see cref="ILlmProvider"/> - swapping vendors touches this file and one
/// registration, nothing else.
/// </summary>
public sealed class OpenAiLlmProvider(IOptions<OpenAiOptions> options) : ILlmProvider
{
    private readonly string _apiKey = options.Value.ApiKey
        ?? throw new InvalidOperationException(
            "Ada:OpenAI:ApiKey is not configured. Set it with user-secrets in development " +
            "or the Ada__OpenAI__ApiKey environment variable in production.");

    public string Name => "openai";

    public async IAsyncEnumerable<LlmDelta> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = new ChatClient(request.Model, new ApiKeyCredential(_apiKey));

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
            if (update.Usage is { } usage)
            {
                yield return new LlmDelta(
                    null,
                    new LlmUsage(usage.InputTokenCount, usage.OutputTokenCount));
            }
        }
    }

    private static ChatMessage ToChatMessage(LlmMessage message) => message.Role switch
    {
        ChatRole.System => new SystemChatMessage(message.Content),
        ChatRole.Assistant => new AssistantChatMessage(message.Content),
        _ => new UserChatMessage(message.Content),
    };
}
