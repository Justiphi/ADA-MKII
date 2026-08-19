using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_Web.Auth;

/// <summary>
/// A session that survives a reload, for an unattended display.
///
/// <see cref="ScopedSessionStore"/> is right for a browser: the token lives only
/// as long as the circuit, so a reload means signing in again rather than
/// leaving a bearer token somewhere a script could read it. A wall-mounted
/// mirror inverts that trade entirely - it reboots, Chromium restarts, and there
/// is no keyboard to sign in with. Without this it would sit on the login page
/// forever.
///
/// The token is provisioned out of band and read from configuration, exactly as
/// the Discord head takes its own from an environment variable. It is a device
/// credential, not a password: it grants one account's access, is revocable by
/// name from the server, and never reaches the browser - every ADA request is
/// still made from this process.
///
/// **Only configure this on a device you physically control.** Anyone who can
/// reach the page is that account, with no login.
/// </summary>
public sealed class KioskSessionStore(string configuredToken) : ISessionStore
{
    private string? _token;

    /// <summary>
    /// Set when the circuit signed out explicitly. The configured token is then
    /// suppressed for the rest of this circuit, so "Sign out" does what it says,
    /// while a fresh circuit - the reboot case - still comes back signed in.
    /// </summary>
    private bool _signedOut;

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null)
        {
            return Task.FromResult<string?>(_token);
        }

        return Task.FromResult(_signedOut ? null : configuredToken);
    }

    public Task SetTokenAsync(string token, CancellationToken cancellationToken)
    {
        _token = token;
        _signedOut = false;

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        _token = null;
        _signedOut = true;

        return Task.CompletedTask;
    }
}
