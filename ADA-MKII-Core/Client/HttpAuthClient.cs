using System.Net;
using System.Net.Http.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Client;

/// <summary>
/// Login over HTTP. On success the token is handed to <see cref="ISessionStore"/>,
/// which is what every later request picks up - the caller never has to route the
/// token anywhere itself.
/// </summary>
public sealed class HttpAuthClient(HttpClient http, ISessionStore session)
    : AuthenticatedHttpClient(http, session), IAuthClient
{
    public async Task<LoginResponse?> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            ApiRoutes.Auth.Login,
            new LoginRequest(username, password),
            cancellationToken);

        // Rejected credentials are an ordinary outcome; anything else is a fault
        // worth throwing over.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return null;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken)
            ?? throw new AdaApiException(response.StatusCode, "Server returned an empty login response.");

        await Session.SetTokenAsync(result.Token, cancellationToken);
        return result;
    }

    public async Task<AccountDto?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, ApiRoutes.Auth.Me, null, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
        {
            return null;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AccountDto>(cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Post, ApiRoutes.Auth.Logout, null, cancellationToken);
            _ = response;
        }
        catch (HttpRequestException)
        {
            // The local session is cleared regardless: a user who pressed logout
            // must end up logged out even if the server is unreachable. The
            // server-side token simply expires unused.
        }

        await Session.ClearAsync(cancellationToken);
    }
}
