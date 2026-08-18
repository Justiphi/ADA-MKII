namespace ADA_MKII_Core.Contracts;

/// <summary>
/// Something ADA decided was worth keeping across conversations.
///
/// Distinct from a note: a note is something the user authored and will read
/// again, a memory is context ADA keeps about the user. Keeping them apart means
/// the notes list does not fill up with the assistant's own bookkeeping.
/// </summary>
public sealed record MemoryDto(
    Guid Id,
    string Content,
    string? Tag,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? LastRecalledUtc);

public sealed record CreateMemoryRequest(string Content, string? Tag);
