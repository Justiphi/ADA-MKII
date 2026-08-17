using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_DataManager;

/// <summary>A user's data footprint, for the operator to see before deleting anything.</summary>
public sealed record AccountUsage(int Conversations, int Messages, long Tokens, int ActiveTokens, DateTimeOffset? LastActivityUtc);

/// <summary>A conversation belonging to some account, as listed in the admin UI.</summary>
public sealed record ConversationSummary(Guid Id, string Title, int MessageCount, long Tokens, DateTimeOffset UpdatedUtc);

/// <summary>
/// Everything the operator tool does against the database.
///
/// This deliberately does not use the account-scoped stores: those filter to the
/// current user, which is exactly wrong for an admin tool that acts across every
/// account. It creates its own scope per call so the UI never shares a tracked
/// DbContext between operations.
/// </summary>
public sealed class AdminService(IServiceProvider services)
{
    public async Task<IReadOnlyList<AccountDto>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountStore>().ListAsync(cancellationToken);
    }

    /// <summary>Creates an account. Returns null when the username is taken.</summary>
    public async Task<AccountDto?> CreateAccountAsync(
        string username,
        string displayName,
        string password,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .CreateAsync(username, displayName, password, cancellationToken);
    }

    public async Task<bool> SetPasswordAsync(Guid id, string password, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .SetPasswordAsync(id, password, cancellationToken);
    }

    public async Task<bool> SetDisabledAsync(Guid id, bool disabled, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .SetDisabledAsync(id, disabled, cancellationToken);
    }

    public async Task<bool> RenameAsync(Guid id, string displayName, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .RenameAsync(id, displayName, cancellationToken);
    }

    public async Task<bool> DeleteAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .DeleteAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceTokenDto>> ListTokensAsync(Guid accountId, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
            .ListForAccountAsync(accountId, cancellationToken);
    }

    public async Task<bool> RevokeTokenAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
            .RevokeAsync(tokenId, cancellationToken);
    }

    /// <summary>Revokes every live token for an account, forcing all its devices to log in again.</summary>
    public async Task<int> RevokeAllTokensAsync(Guid accountId, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdaDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        return await db.DeviceTokens
            .Where(t => t.AccountId == accountId && t.RevokedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedUtc, now), cancellationToken);
    }

    public async Task<AccountUsage> GetUsageAsync(Guid accountId, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdaDbContext>();

        var conversations = db.Conversations.Where(c => c.AccountId == accountId);
        var messages = db.Messages.Where(m => m.Conversation!.AccountId == accountId);

        return new AccountUsage(
            await conversations.CountAsync(cancellationToken),
            await messages.CountAsync(cancellationToken),
            await messages.SumAsync(m => (long)m.TokensIn + m.TokensOut, cancellationToken),
            await db.DeviceTokens.CountAsync(t => t.AccountId == accountId && t.RevokedUtc == null, cancellationToken),
            await conversations.MaxAsync(c => (DateTimeOffset?)c.UpdatedUtc, cancellationToken));
    }

    public async Task<IReadOnlyList<ConversationSummary>> ListConversationsAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdaDbContext>();

        return await db.Conversations
            .AsNoTracking()
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.UpdatedUtc)
            .Select(c => new ConversationSummary(
                c.Id,
                c.Title,
                c.Messages.Count,
                c.Messages.Sum(m => (long)m.TokensIn + m.TokensOut),
                c.UpdatedUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdaDbContext>();

        var deleted = await db.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    /// <summary>Deletes all of an account's conversations while keeping the account itself.</summary>
    public async Task<int> DeleteAllConversationsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdaDbContext>();

        return await db.Conversations
            .Where(c => c.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Verifies the database is reachable and migrated, so failures surface at
    /// startup rather than on the operator's first click. Names the server and
    /// database it tried, because "cannot connect" without those is unactionable.
    /// </summary>
    public async Task<string> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdaDbContext>();

        var target = Describe(db.Database.GetConnectionString());

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return $"Cannot reach {target}. Check the server is running and the connection string is right.";
        }

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pendingCount = pending.Count();

        return pendingCount > 0
            ? $"Connected to {target}, but {pendingCount} migration(s) are pending. Run 'dotnet ef database update'."
            : $"Connected to {target}.";
    }

    /// <summary>Server and database only - never the whole string, which may carry a password.</summary>
    private static string Describe(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "the database";
        }

        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            return $"{builder.DataSource}/{builder.InitialCatalog}";
        }
        catch (ArgumentException)
        {
            return "the database";
        }
    }
}
