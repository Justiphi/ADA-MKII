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
    IToolRegistry tools,
    IMemoryStore memories,
    AssistantTurnContext turn,
    IOptions<AssistantOptions> options,
    TimeProvider clock,
    ILogger<AssistantPipeline> logger) : IAssistantPipeline
{
    private readonly AssistantOptions _defaults = options.Value;

    /// <summary>
    /// How many times the model may call tools and be asked again within one
    /// turn. Every iteration is a billed model call, and a confused model will
    /// happily loop, so this is deliberately small: enough for "look it up, then
    /// act on what you found, then answer", not enough to run away.
    /// </summary>
    private const int MaxToolIterations = 4;

    /// <summary>
    /// How many memories to put in front of the model unprompted. Enough to feel
    /// like it knows the user, few enough that it is not paying to re-read its
    /// whole memory every turn - recall exists for the rest.
    /// </summary>
    private const int AmbientMemoryCount = 8;

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

        // Everything time-related in this turn - the prompt's "now", and any date
        // a tool has to resolve - hangs off the caller's zone.
        turn.SetZone(request.TimeZoneId);

        var model = await ResolveAsync(SettingKeys.Model, _defaults.Model, cancellationToken);
        var systemPrompt = await ResolveAsync(SettingKeys.SystemPrompt, _defaults.SystemPrompt, cancellationToken);
        var temperature = await ResolveDoubleAsync(SettingKeys.Temperature, _defaults.Temperature, cancellationToken);
        var maxTokens = await ResolveIntAsync(SettingKeys.MaxTokensPerTurn, _defaults.MaxOutputTokens, cancellationToken);
        var historyLimit = await ResolveIntAsync(SettingKeys.HistoryMessageLimit, _defaults.HistoryMessageLimit, cancellationToken);
        var budget = await ResolveLongAsync(SettingKeys.MonthlyTokenBudget, _defaults.MonthlyTokenBudget, cancellationToken);
        var endpoint = await ResolveEndpointAsync(cancellationToken);
        var toolsEnabled = await ResolveBoolAsync(SettingKeys.ToolsEnabled, true, cancellationToken);

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

        var grounding = await BuildGroundingAsync(cancellationToken);
        var history = BuildPrompt(systemPrompt, grounding, conversation.Messages, historyLimit, request.Message);

        var buffer = new StringBuilder();
        var tokensIn = 0;
        var tokensOut = 0;

        // The tool loop: stream a reply, and if the model asked for tools instead
        // of answering, run them, append what they said, and ask again. Text and
        // tool calls are not exclusive - a model may narrate before acting - so
        // anything it says along the way is streamed through and kept.
        for (var iteration = 0; ; iteration++)
        {
            var llmRequest = new LlmRequest(model, history, maxTokens, temperature)
            {
                Endpoint = endpoint,

                // Stop offering tools on the final iteration, which forces the
                // model to produce prose rather than asking for a call that
                // would have nowhere to go.
                Tools = toolsEnabled && iteration < MaxToolIterations ? tools.Definitions : [],
            };

            var calls = new List<LlmToolCall>();
            var spoken = new StringBuilder();

            await foreach (var delta in llm.StreamAsync(llmRequest, cancellationToken))
            {
                if (delta.Usage is { } u)
                {
                    // Summed, not replaced: every iteration is separately billed,
                    // and the budget only means anything if it counts all of them.
                    tokensIn += u.TokensIn;
                    tokensOut += u.TokensOut;
                }

                if (!string.IsNullOrEmpty(delta.Text))
                {
                    spoken.Append(delta.Text);
                    buffer.Append(delta.Text);
                    yield return ChatStreamEvent.Delta(delta.Text);
                }

                if (delta.ToolCalls is { Count: > 0 } requested)
                {
                    calls.AddRange(requested);
                }
            }

            if (calls.Count == 0)
            {
                break;
            }

            if (iteration >= MaxToolIterations)
            {
                // Belt and braces: Tools was already emptied above, so a model
                // that still asks is one ignoring the request.
                CoreLog.ToolLoopExhausted(logger, MaxToolIterations);
                break;
            }

            // The model has to see its own request replayed, or the results
            // below pair with nothing.
            history.Add(new LlmMessage(ChatRole.Assistant, spoken.ToString()) { ToolCalls = calls });

            foreach (var call in calls)
            {
                yield return ChatStreamEvent.Tool(call.Name);

                var result = await tools.InvokeAsync(call.Name, call.ArgumentsJson, cancellationToken);

                history.Add(new LlmMessage(ChatRole.Tool, result.Content) { ToolCallId = call.Id });
            }
        }

        // Reached only on a completed stream. If the client disconnects, the
        // enumerator is cancelled and we never get here - the partial reply is
        // discarded, which is the point: the turn stops burning tokens.
        var finalUsage = new LlmUsage(tokensIn, tokensOut);
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
        string grounding,
        IReadOnlyList<ChatMessageDto> history,
        int limit,
        string newMessage)
    {
        var messages = new List<LlmMessage>(capacity: limit + 3)
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.System, grounding),
        };

        // Only user and assistant turns are replayed. Tool traffic belongs to the
        // turn that produced it: replaying a stored tool result without the call
        // it answered is malformed, and the assistant's prose already says what
        // came of it.
        var conversational = history.Where(m => m.Role is ChatRole.User or ChatRole.Assistant).ToList();
        var recent = conversational.Count > limit ? conversational.Skip(conversational.Count - limit) : conversational;

        messages.AddRange(recent.Select(m => new LlmMessage(m.Role, m.Content)));
        messages.Add(new LlmMessage(ChatRole.User, newMessage));

        return messages;
    }

    /// <summary>
    /// The two things the model cannot work out for itself: what time it is where
    /// the user is, and what it already knows about them.
    ///
    /// Without the first, "remind me tomorrow at 3" is unanswerable. The second
    /// is here rather than left to the recall tool because the common case is
    /// wanting one fact in passing, and spending a whole billed iteration on a
    /// tool call to get it is worse than sending eight short lines.
    /// </summary>
    private async Task<string> BuildGroundingAsync(CancellationToken cancellationToken)
    {
        var local = turn.LocalNow;

        var grounding = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"The user's local time is {local:dddd d MMMM yyyy, HH:mm} ({turn.Zone.Id}). ")
            .Append("Interpret and answer with times in that zone, and never convert them to UTC yourself.");

        // A failure to read memories must not take the turn down with it - the
        // model can still be useful without them, and recall remains available.
        IReadOnlyList<MemoryDto> recent;

        try
        {
            recent = await memories.ListRecentAsync(AmbientMemoryCount, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return grounding.ToString();
        }

        if (recent.Count == 0)
        {
            return grounding.ToString();
        }

        grounding.AppendLine().AppendLine().Append("What you already know about the user:");

        foreach (var memory in recent)
        {
            grounding.AppendLine().Append(CultureInfo.InvariantCulture, $"- {memory.Content}");
        }

        return grounding.ToString();
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

    private async Task<bool> ResolveBoolAsync(string key, bool fallback, CancellationToken ct)
    {
        var raw = await settings.GetAsync<string>(key, ct);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }

    private async Task<double> ResolveDoubleAsync(string key, double fallback, CancellationToken ct)
    {
        var raw = await settings.GetAsync<string>(key, ct);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
