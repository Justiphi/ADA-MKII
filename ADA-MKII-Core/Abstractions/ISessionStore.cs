using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Where a head keeps the token it got at login. Each platform answers this
/// differently - SecureStorage on MAUI, protected server-side state on the web
/// head - which is exactly why the shared UI talks to an interface instead.
/// </summary>
public interface ISessionStore
{
    Task<string?> GetTokenAsync(CancellationToken cancellationToken);

    Task SetTokenAsync(string token, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}

/// <summary>Performs the login exchange. Implemented in Core over HTTP.</summary>
public interface IAuthClient
{
    /// <summary>
    /// Exchanges credentials for a token. Returns null when the credentials are
    /// rejected - a wrong password is an expected outcome, not an exception.
    /// </summary>
    Task<LoginResponse?> LoginAsync(string username, string password, CancellationToken cancellationToken);

    Task<AccountDto?> GetCurrentAsync(CancellationToken cancellationToken);

    Task LogoutAsync(CancellationToken cancellationToken);
}
