using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// What ADA remembers about the user between conversations. Account-scoped like
/// every other store.
/// </summary>
public interface IMemoryStore
{
    Task<MemoryDto> CreateAsync(CreateMemoryRequest request, CancellationToken cancellationToken);

    /// <summary>Most recent first. Used to give the model ambient context each turn.</summary>
    Task<IReadOnlyList<MemoryDto>> ListRecentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Keyword search, marking what it returns as recalled. Good to a few hundred
    /// memories; past that, recall quality is the reason to reach for embeddings.
    /// </summary>
    Task<IReadOnlyList<MemoryDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
