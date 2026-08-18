namespace ADA_MKII_Data.Entities;

/// <summary>
/// Something ADA chose to remember about the user. The most sensitive text in the
/// database after the conversation transcript itself.
/// </summary>
public sealed class MemoryEntity
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public AccountEntity? Account { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>Optional loose category - "preference", "person", "project".</summary>
    public string? Tag { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When it was last returned by a search. Lets stale memories be found and
    /// pruned later without guessing at what mattered.
    /// </summary>
    public DateTimeOffset? LastRecalledUtc { get; set; }
}
