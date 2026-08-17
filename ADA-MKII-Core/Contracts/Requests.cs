namespace ADA_MKII_Core.Contracts;

/// <summary>Create a new conversation.</summary>
public sealed record CreateConversationRequest(string? Title);

/// <summary>Append a message to an existing conversation.</summary>
public sealed record AppendMessageRequest(ChatRole Role, string Content, int TokensIn = 0, int TokensOut = 0);

/// <summary>Set a single non-secret preference.</summary>
public sealed record SetSettingRequest(string Value);
