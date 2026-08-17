namespace ADA_MKII_Core.Contracts;

/// <summary>What a client asks for when it wants a reply.</summary>
public sealed record ChatRequest(Guid? ConversationId, string Message);

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

    public static ChatStreamEvent Delta(string text) => new(DeltaType, Text: text);

    public static ChatStreamEvent Done(Guid conversationId, Guid messageId, LlmUsage usage) =>
        new(DoneType,
            ConversationId: conversationId,
            MessageId: messageId,
            TokensIn: usage.TokensIn,
            TokensOut: usage.TokensOut);

    public static ChatStreamEvent Failed(string error) => new(ErrorType, Error: error);
}
