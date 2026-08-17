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

        var entity = await db.DeviceTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedUtc == null, cancellationToken);

        return entity is null
            ? null
            : new DeviceTokenDto(entity.Id, entity.Name, entity.CreatedUtc, entity.LastSeenUtc, entity.RevokedUtc);
    }

    public Task TouchAsync(Guid id, CancellationToken cancellationToken) =>
        db.DeviceTokens
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastSeenUtc, clock.GetUtcNow()), cancellationToken);

    public async Task<bool> EnsureAsync(string name, string tokenHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (await db.DeviceTokens.AnyAsync(t => t.Name == name, cancellationToken))
        {
            return false;
        }

        db.DeviceTokens.Add(new DeviceTokenEntity
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            TokenHash = tokenHash,
            CreatedUtc = clock.GetUtcNow(),
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
