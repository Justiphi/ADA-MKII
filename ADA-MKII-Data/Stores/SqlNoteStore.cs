using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data.Stores;

/// <summary>
/// Server-side <see cref="INoteStore"/>. Every query filters by
/// <see cref="IAccountContext.AccountId"/>, so a caller cannot reach another
/// account's notes even by guessing an id.
/// </summary>
public sealed class SqlNoteStore(AdaDbContext db, IAccountContext account, TimeProvider clock) : INoteStore
{
    public async Task<NoteDto> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = clock.GetUtcNow();
        var entity = new NoteEntity
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.AccountId,
            Title = DeriveTitle(request.Title, request.Content),
            Content = request.Content ?? string.Empty,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        db.Notes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<NoteDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.AccountId == account.AccountId, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<NoteDto>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        var entities = await db.Notes
            .AsNoTracking()
            .Where(n => n.AccountId == account.AccountId)
            .OrderByDescending(n => n.UpdatedUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDto)];
    }

    public async Task<IReadOnlyList<NoteDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await ListAsync(limit, cancellationToken);
        }

        // LIKE rather than ToLower: SQL Server's default collation is already
        // case-insensitive, and ToLower in a predicate defeats any index.
        var pattern = "%" + query.Trim() + "%";

        var entities = await db.Notes
            .AsNoTracking()
            .Where(n => n.AccountId == account.AccountId
                && (EF.Functions.Like(n.Title, pattern) || EF.Functions.Like(n.Content, pattern)))
            .OrderByDescending(n => n.UpdatedUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDto)];
    }

    public async Task<NoteDto?> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await db.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.AccountId == account.AccountId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.Title = DeriveTitle(request.Title, request.Content);
        entity.Content = request.Content ?? string.Empty;
        entity.UpdatedUtc = clock.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.Notes
            .Where(n => n.Id == id && n.AccountId == account.AccountId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    /// <summary>
    /// A note dictated in passing has no title, so derive one from the opening
    /// line rather than showing a blank row in the list.
    /// </summary>
    private static string DeriveTitle(string? supplied, string? content)
    {
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            var trimmed = supplied.Trim();
            return trimmed.Length <= 200 ? trimmed : trimmed[..200];
        }

        var firstLine = (content ?? string.Empty)
            .Trim()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim() ?? string.Empty;

        if (firstLine.Length == 0)
        {
            return "Untitled note";
        }

        return firstLine.Length <= 60 ? firstLine : firstLine[..57] + "...";
    }

    private static NoteDto ToDto(NoteEntity e) =>
        new(e.Id, e.Title, e.Content, e.CreatedUtc, e.UpdatedUtc);
}
