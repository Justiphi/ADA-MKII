using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Core.Security;
using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data.Stores;

/// <summary>
/// Account management against SQL Server. Used by ADA-MKII-DataManager to create
/// and administer accounts, and by ADA-MKII-Server to look one up at login.
/// </summary>
public sealed class SqlAccountStore(AdaDbContext db, TimeProvider clock) : IAccountStore
{
    public async Task<AccountDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken cancellationToken)
    {
        var entities = await db.Accounts
            .AsNoTracking()
            .OrderBy(a => a.Username)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDto)];
    }

    public async Task<(AccountDto Account, string PasswordHash)?> FindForLoginAsync(
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalised = Normalise(username);
        var entity = await db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Username == normalised, cancellationToken);

        return entity is null ? null : (ToDto(entity), entity.PasswordHash);
    }

    public async Task<AccountDto?> CreateAsync(
        string username,
        string displayName,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var normalised = Normalise(username);

        if (await db.Accounts.AnyAsync(a => a.Username == normalised, cancellationToken))
        {
            return null;
        }

        var entity = new AccountEntity
        {
            Id = Guid.CreateVersion7(),
            Username = normalised,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username.Trim() : displayName.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            CreatedUtc = clock.GetUtcNow(),
        };

        db.Accounts.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<bool> SetPasswordAsync(Guid id, string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var hash = PasswordHasher.Hash(password);
        var updated = await db.Accounts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PasswordHash, hash), cancellationToken);

        if (updated == 0)
        {
            return false;
        }

        // A password change must not leave old sessions alive.
        await db.DeviceTokens
            .Where(t => t.AccountId == id && t.RevokedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedUtc, clock.GetUtcNow()), cancellationToken);

        return true;
    }

    public async Task<bool> SetDisabledAsync(Guid id, bool disabled, CancellationToken cancellationToken)
    {
        DateTimeOffset? value = disabled ? clock.GetUtcNow() : null;

        var updated = await db.Accounts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.DisabledUtc, value), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> RenameAsync(Guid id, string displayName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var trimmed = displayName.Trim();
        var updated = await db.Accounts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.DisplayName, trimmed), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        // Conversations, settings and tokens cascade from the account.
        var deleted = await db.Accounts.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    /// <summary>Usernames are compared case-insensitively, so they are stored lowercase.</summary>
    private static string Normalise(string username) => username.Trim().ToLowerInvariant();

    private static AccountDto ToDto(AccountEntity entity) =>
        new(entity.Id, entity.Username, entity.DisplayName, entity.CreatedUtc, entity.DisabledUtc);
}
