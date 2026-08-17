using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_Web.Auth;

/// <summary>
/// Holds the token for one Blazor Server circuit, in server memory.
///
/// The token is never rendered into the page or handed to JavaScript: the browser
/// only ever holds the circuit's own connection, and every ADA request is made
/// server-side. The cost is that a page reload starts a new circuit and so
/// requires signing in again - acceptable for now, and far preferable to putting
/// a bearer token in localStorage where any script on the page could read it.
/// </summary>
public sealed class ScopedSessionStore : ISessionStore
{
    private string? _token;

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult(_token);

    public Task SetTokenAsync(string token, CancellationToken cancellationToken)
    {
        _token = token;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        _token = null;
        return Task.CompletedTask;
    }
}
