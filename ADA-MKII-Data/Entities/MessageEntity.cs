using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Data.Entities;

/// <summary>EF entity for a single conversation message.</summary>
public sealed class MessageEntity
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public ConversationEntity? Conversation { get; set; }

    public ChatRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Prompt tokens billed for this turn. Feeds the monthly spend guard.</summary>
    public int TokensIn { get; set; }

    /// <summary>Completion tokens billed for this turn.</summary>
    public int TokensOut { get; set; }
}
