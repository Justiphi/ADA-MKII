namespace ADA_MKII_Data.Entities;

/// <summary>A note, owned by an account. Every read is scoped by AccountId.</summary>
public sealed class NoteEntity
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public AccountEntity? Account { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}
