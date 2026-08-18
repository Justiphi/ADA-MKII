using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADA_MKII_Core.Pipeline;

/// <summary>
/// One conversational turn end to end: resolve settings, check spend, load and
/// trim history, call the model, stream deltas out, persist the result.
/// </summary>
public sealed class AssistantPipeline(
    ILlmProvider llm,
    IConversationStore conversations,
    ISettingsStore settings,
    IUsageStore usage,
    IOptions<AssistantOptions> options,
    TimeProvider clock,
    ILogger<AssistantPipeline> logger) : IAssistantPipeline
{
    private readonly AssistantOptions _defaults = options.Value;

    public async IAsyncEnumerable<ChatStreamEvent> RunAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            yield return ChatStreamEvent.Failed("Message is required.");
            yield break;
        }

        var model = await ResolveAsync(SettingKeys.Model, _defaults.Model, cancellationToken);
        var systemPrompt = await ResolveAsync(SettingKeys.SystemPrompt, _defaults.SystemPrompt, cancellationToken);
        var temperature = await ResolveDoubleAsync(SettingKeys.Temperature, _defaults.Temperature, cancellationToken);
        var maxTokens = await ResolveIntAsync(SettingKeys.MaxTokensPerTurn, _defaults.MaxOutputTokens, cancellationToken);
        var historyLimit = await ResolveIntAsync(SettingKeys.HistoryMessageLimit, _defaults.HistoryMessageLimit, cancellationToken);
        var budget = await ResolveLongAsync(SettingKeys.MonthlyTokenBudget, _defaults.MonthlyTokenBudget, cancellationToken);
        var endpoint = await ResolveEndpointAsync(cancellationToken);

        // Spend guard first: refusing before the model call is the only way it
        // actually saves money.
        if (budget > 0)
        {
            var now = clock.GetUtcNow();
            var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var used = await usage.GetTokensSinceAsync(monthStart, cancellationToken);

            if (used >= budget)
            {
                CoreLog.BudgetExhausted(logger, used, budget);
                yield return ChatStreamEvent.Failed(
                    $"Monthly token budget reached ({used:N0} of {budget:N0}). Raise '{SettingKeys.MonthlyTokenBudget}' to continue.");
                yield break;
            }
        }

        var conversation = request.ConversationId is { } id
            ? await conversations.GetAsync(id, cancellationToken)
            : await conversations.CreateAsync(Summarise(request.Message), cancellationToken);

        if (conversation is null)
        {
            yield return ChatStreamEvent.Failed("Conversation not found.");
            yield break;
        }

        // Persist the user's message before calling the model, so a failure or a
        // disconnect mid-stream cannot lose what they said.
        await conversations.AppendMessageAsync(
            conversation.Id,
            new AppendMessageRequest(ChatRole.User, request.Message),
            cancellationToken);

        var history = BuildPrompt(systemPrompt, conversation.Messages, historyLimit, request.Message);
        var llmRequest = new LlmRequest(model, history, maxTokens, temperature) { Endpoint = endpoint };

        var buffer = new StringBuilder();
        LlmUsage? reported = null;

        await foreach (var delta in llm.StreamAsync(llmRequest, cancellationToken))
        {
            if (delta.Usage is { } u)
            {
                reported = u;
            }

            if (!string.IsNullOrEmpty(delta.Text))
            {
                buffer.Append(delta.Text);
                yield return ChatStreamEvent.Delta(delta.Text);
            }
        }

        // Reached only on a completed stream. If the client disconnects, the
        // enumerator is cancelled and we never get here - the partial reply is
        // discarded, which is the point: the turn stops burning tokens.
        var finalUsage = reported ?? new LlmUsage(0, 0);
        var text = buffer.ToString().Trim();

        var saved = await conversations.AppendMessageAsync(
            conversation.Id,
            new AppendMessageRequest(ChatRole.Assistant, text, finalUsage.TokensIn, finalUsage.TokensOut),
            cancellationToken);

        CoreLog.TurnComplete(logger, llm.Name, model, finalUsage.TokensIn, finalUsage.TokensOut, history.Count);

        yield return ChatStreamEvent.Done(conversation.Id, saved?.Id ?? Guid.Empty, finalUsage);
    }

    /// <summary>
    /// System prompt, then the tail of the conversation, then the new message.
    /// Trimming keeps the oldest turns out rather than the newest: recent context
    /// is what the model actually needs.
    /// </summary>
    private static List<LlmMessage> BuildPrompt(
        string systemPrompt,
        IReadOnlyList<ChatMessageDto> history,
        int limit,
        string newMessage)
    {
        var messages = new List<LlmMessage>(capacity: limit + 2)
        {
            new(ChatRole.System, systemPrompt),
        };

        var recent = history.Count > limit ? history.Skip(history.Count - limit) : history;
        messages.AddRange(recent.Select(m => new LlmMessage(m.Role, m.Content)));
        messages.Add(new LlmMessage(ChatRole.User, newMessage));

        return messages;
    }

    private static string Summarise(string message) =>
        message.Length <= 60 ? message : message[..57] + "...";

    /// <summary>
    /// The endpoint the user chose, if any. Rejected unless it is an absolute
    /// http/https URL: the server is what makes this call, so an address that
    /// arrives from a settings page is a request this server will issue on
    /// someone else's say-so. A bad value falls back to the server's own
    /// configuration rather than failing the turn.
    /// </summary>
    private async Task<Uri?> ResolveEndpointAsync(CancellationToken ct)
    {
        var raw = await settings.GetAsync<string>(SettingKeys.BaseUrl, ct);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https" ? uri : null;
    }

    private async Task<string> ResolveAsync(string key, string fallback, CancellationToken ct)
    {
        var raw = await settings.GetAsync<string>(key, ct);
        return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
    }

    private async Task<int> ResolveIntAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await settings.GetAsync<string>(key, ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private async Task<long> ResolveLongAsync(string key, long fallback, CancellationToken ct)
    {
        var raw = await settings.GetAsync<string>(key, ct);
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private async Task<double> ResolveDoubleAsync(string key, double fallback, CancellationToken ct)
    {
        var raw = await settings.GetAsync<string>(key, ct);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
