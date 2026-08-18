using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data.Stores;

/// <summary>Server-side <see cref="IMemoryStore"/>, scoped to the current account.</summary>
public sealed class SqlMemoryStore(AdaDbContext db, IAccountContext account, TimeProvider clock) : IMemoryStore
{
    public async Task<MemoryDto> CreateAsync(CreateMemoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Content);

        var entity = new MemoryEntity
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.AccountId,
            Content = request.Content.Trim(),
            Tag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim(),
            CreatedUtc = clock.GetUtcNow(),
        };

        db.Memories.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<IReadOnlyList<MemoryDto>> ListRecentAsync(int limit, CancellationToken cancellationToken)
    {
        var entities = await db.Memories
            .AsNoTracking()
            .Where(m => m.AccountId == account.AccountId)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDto)];
    }

    public async Task<IReadOnlyList<MemoryDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(limit, 1, 200);

        // Match any significant word rather than the whole phrase: a question
        // rarely repeats the exact wording the memory was written in.
        var words = (query ?? string.Empty)
            .Split([' ', ',', '.', '?', '!', ';', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        if (words.Count == 0)
        {
            return await ListRecentAsync(take, cancellationToken);
        }

        // LIKE rather than ToLower: SQL Server's default collation is already
        // case-insensitive, and lowering a column in a predicate defeats indexes.
        var patterns = words.Select(w => "%" + w + "%").ToList();

        var entities = await db.Memories
            .AsNoTracking()
            .Where(m => m.AccountId == account.AccountId)
            .Where(m => patterns.Any(p => EF.Functions.Like(m.Content, p)
                || (m.Tag != null && EF.Functions.Like(m.Tag, p))))
            .OrderByDescending(m => m.CreatedUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (entities.Count > 0)
        {
            // Recording the recall makes it possible to find stale memories later
            // without having to guess which ones mattered.
            var ids = entities.Select(e => e.Id).ToList();
            var now = clock.GetUtcNow();

            await db.Memories
                .Where(m => ids.Contains(m.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LastRecalledUtc, now), cancellationToken);
        }

        return [.. entities.Select(ToDto)];
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.Memories
            .Where(m => m.Id == id && m.AccountId == account.AccountId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    private static MemoryDto ToDto(MemoryEntity e) =>
        new(e.Id, e.Content, e.Tag, e.CreatedUtc, e.LastRecalledUtc);
}
