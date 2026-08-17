using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_Core.Client;

/// <summary>
/// Base for the HTTP clients, attaching the bearer token to each request.
///
/// The token is read here rather than in a DelegatingHandler on purpose.
/// IHttpClientFactory creates message handlers in the handler pool's own DI
/// scope, not the caller's, so a handler that resolves a scoped ISessionStore
/// gets a *different* instance than the one the user signed in on - the request
/// then goes out unauthenticated and the failure looks like a bad token rather
/// than a wiring mistake. Typed clients are resolved from the calling scope, so
/// reading the session here is correct on every head.
/// </summary>
public abstract class AuthenticatedHttpClient(HttpClient http, ISessionStore session)
{
    protected HttpClient Http { get; } = http;

    /// <summary>Exposed so a derived client can also write to the session, as login does.</summary>
    protected ISessionStore Session { get; } = session;

    protected async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string uri,
        object? payload,
        CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        using var request = new HttpRequestMessage(method, uri);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, payload.GetType());
        }

        var token = await Session.GetTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            return await Http.SendAsync(request, completionOption, cancellationToken);
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            // Collapse the several ways "the server is not there" arrives into
            // one exception, so callers do not have to know about the transport
            // or the resilience pipeline to handle it.
            throw new AdaUnreachableException(Http.BaseAddress, ex);
        }
    }

    private static bool IsUnreachable(Exception exception, CancellationToken cancellationToken)
    {
        // A caller that cancelled deliberately - a stopped chat turn, a closed
        // page - has not encountered an unreachable server.
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is HttpRequestException
            or TaskCanceledException
            or TimeoutException
            // Polly's timeout, raised when the resilience pipeline exhausts its
            // budget. Matched by name so this type does not leak into callers.
            || exception.GetType().FullName == "Polly.Timeout.TimeoutRejectedException";
    }
}
