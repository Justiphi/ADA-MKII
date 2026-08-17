using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_UI.Auth;

/// <summary>
/// Keeps the token in the platform's encrypted store, so it survives a restart
/// and a user is not asked to sign in every launch. This is the one secret a
/// device holds - see CLAUDE.md.
/// </summary>
public sealed class SecureStorageSessionStore : ISessionStore
{
    /// <summary>SecureStorage key holding this device's bearer token.</summary>
    public const string TokenKey = "ada.deviceToken";

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // SecureStorage throws if the platform keystore is unavailable, or if
            // an existing value was written under different device credentials.
            // Treating that as "not signed in" sends the user to the login page,
            // which is recoverable; rethrowing would just crash the app on launch.
            SecureStorage.Default.Remove(TokenKey);
            return null;
        }
    }

    public async Task SetTokenAsync(string token, CancellationToken cancellationToken) =>
        await SecureStorage.Default.SetAsync(TokenKey, token);

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        SecureStorage.Default.Remove(TokenKey);
        return Task.CompletedTask;
    }
}
