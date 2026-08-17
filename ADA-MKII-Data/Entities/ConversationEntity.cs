namespace ADA_MKII_Data.Entities;

/// <summary>
/// EF entity. Entities are internal to ADA-MKII-Data by convention: stores map
/// them to Core DTOs and it is the DTOs, never these, that cross the wire.
/// </summary>
public sealed class ConversationEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning account. Every read is scoped by this - see IAccountContext.</summary>
    public Guid AccountId { get; set; }

    public AccountEntity? Account { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public ICollection<MessageEntity> Messages { get; } = [];
}
