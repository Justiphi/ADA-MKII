namespace ADA_MKII_Core.Contracts;

/// <summary>A note the user wrote, or asked ADA to write down.</summary>
public sealed record NoteDto(
    Guid Id,
    string Title,
    string Content,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record CreateNoteRequest(string? Title, string Content);

public sealed record UpdateNoteRequest(string? Title, string Content);
