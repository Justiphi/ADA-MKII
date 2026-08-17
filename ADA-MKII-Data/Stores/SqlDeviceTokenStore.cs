using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data.Stores;

/// <summary>Server-side <see cref="IDeviceTokenStore"/>. Only hashes are ever persisted.</summary>
public sealed class SqlDeviceTokenStore(AdaDbContext db, TimeProvider clock) : IDeviceTokenStore
{
    public async Task<DeviceTokenDto?> FindActiveAsync(string tokenHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        // The account check is part of the lookup, not a separate step: disabling
        // an account has to invalidate its existing tokens immediately, and a
        // second query would be a window where it did not.
        var entity = await db.DeviceTokens
            .AsNoTracking()
            .Where(t => t.TokenHash == tokenHash
                && t.RevokedUtc == null
                && t.Account!.DisabledUtc == null)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public Task TouchAsync(Guid id, CancellationToken cancellationToken) =>
        db.DeviceTokens
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastSeenUtc, clock.GetUtcNow()), cancellationToken);

    public async Task<DeviceTokenDto> CreateAsync(
        Guid accountId,
        string name,
        string tokenHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var entity = new DeviceTokenEntity
        {
            Id = Guid.CreateVersion7(),
            AccountId = accountId,
            Name = name.Trim(),
            TokenHash = tokenHash,
            CreatedUtc = clock.GetUtcNow(),
        };

        db.DeviceTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<IReadOnlyList<DeviceTokenDto>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var entities = await db.DeviceTokens
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedUtc)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDto)];
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        var updated = await db.DeviceTokens
            .Where(t => t.Id == id && t.RevokedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedUtc, clock.GetUtcNow()), cancellationToken);

        return updated > 0;
    }

    private static DeviceTokenDto ToDto(DeviceTokenEntity e) =>
        new(e.Id, e.AccountId, e.Name, e.CreatedUtc, e.LastSeenUtc, e.RevokedUtc);
}
