using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Account management. Deliberately has no HTTP implementation: accounts are
/// created only by ADA-MKII-DataManager talking straight to the database, so
/// there is no self-registration surface on the public API to attack.
/// </summary>
public interface IAccountStore
{
    Task<AccountDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Looks up an account and returns its stored password hash for verification.
    /// The hash is returned only here, and only ever inside the server process.
    /// </summary>
    Task<(AccountDto Account, string PasswordHash)?> FindForLoginAsync(string username, CancellationToken cancellationToken);

    /// <summary>Creates an account. Returns null if the username is already taken.</summary>
    Task<AccountDto?> CreateAsync(string username, string displayName, string password, CancellationToken cancellationToken);

    Task<bool> SetPasswordAsync(Guid id, string password, CancellationToken cancellationToken);

    /// <summary>Disabling is preferred over deletion: it keeps the account's history intact.</summary>
    Task<bool> SetDisabledAsync(Guid id, bool disabled, CancellationToken cancellationToken);

    Task<bool> RenameAsync(Guid id, string displayName, CancellationToken cancellationToken);

    /// <summary>Deletes the account and everything it owns. Irreversible.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
