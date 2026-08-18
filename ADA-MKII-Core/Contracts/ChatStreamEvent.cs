namespace ADA_MKII_Core.Contracts;

/// <summary>What a client asks for when it wants a reply.</summary>
public sealed record ChatRequest(Guid? ConversationId, string Message)
{
    /// <summary>
    /// The caller's IANA time zone, e.g. "Pacific/Auckland". Null means UTC.
    ///
    /// It comes from the client because only the client knows: on the web head
    /// the server is a VPS, and its zone says nothing about where the user is.
    /// Without this, "remind me tomorrow at 3" cannot be resolved.
    /// </summary>
    public string? TimeZoneId { get; init; }
}

/// <summary>
/// One server-sent event on the chat stream. A single flat record rather than a
/// type hierarchy, because this is serialised to JSON over SSE and consumed by a
/// browser as well as by C# - a polymorphic shape would buy nothing and cost
/// converter configuration on both ends.
/// </summary>
public sealed record ChatStreamEvent(
    string Type,
    string? Text = null,
    Guid? ConversationId = null,
    Guid? MessageId = null,
    int? TokensIn = null,
    int? TokensOut = null,
    string? Error = null)
{
    public const string DeltaType = "delta";
    public const string DoneType = "done";
    public const string ErrorType = "error";

    /// <summary>ADA is doing something rather than talking. Carries the tool name.</summary>
    public const string ToolType = "tool";

    public static ChatStreamEvent Delta(string text) => new(DeltaType, Text: text);

    public static ChatStreamEvent Done(Guid conversationId, Guid messageId, LlmUsage usage) =>
        new(DoneType,
            ConversationId: conversationId,
            MessageId: messageId,
            TokensIn: usage.TokensIn,
            TokensOut: usage.TokensOut);

    /// <summary>
    /// Announced before the tool runs, not after, so the UI can say what is
    /// happening during the pause rather than explaining it once it is over.
    /// </summary>
    public static ChatStreamEvent Tool(string name) => new(ToolType, Text: name);

    public static ChatStreamEvent Failed(string error) => new(ErrorType, Error: error);
}
