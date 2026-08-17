namespace ADA_MKII_Core.Contracts;

/// <summary>A conversation and, when fetched by id, its messages.</summary>
public sealed record ConversationDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    IReadOnlyList<ChatMessageDto> Messages);
