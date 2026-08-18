using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Note persistence. Like <see cref="IConversationStore"/>, no method takes an
/// account id: the server-side implementation scopes by
/// <see cref="IAccountContext"/>, so there is no argument in which a caller
/// could name someone else's records.
/// </summary>
public interface INoteStore
{
    Task<NoteDto> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken);

    Task<NoteDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<NoteDto>> ListAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Substring match over title and content, newest first.</summary>
    Task<IReadOnlyList<NoteDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    Task<NoteDto?> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
