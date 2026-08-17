using System.Security.Claims;
using ADA_MKII_Core;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Core.Security;
using ADA_MKII_Server.Auth;
using ADA_MKII_Server.Logging;

namespace ADA_MKII_Server.Endpoints;

/// <summary>
/// Login and session management.
///
/// There is deliberately no registration endpoint: accounts are created solely by
/// ADA-MKII-DataManager against the database, so the public API offers no way to
/// create one no matter what an attacker sends.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ApiRoutes.Auth.Login, async (
            LoginRequest request,
            IAccountStore accounts,
            IDeviceTokenStore tokens,
            ILoggerFactory loggerFactory,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Ada.Auth");

            if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.Problem(title: "Username and password are required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var found = await accounts.FindForLoginAsync(request.Username, cancellationToken);

            // Verify a password even when the account does not exist, so that a
            // wrong username and a wrong password take the same time. Skipping
            // the hash for unknown users turns login into a username oracle.
            var passwordHash = found?.PasswordHash ?? PasswordHasher.DummyHash;
            var passwordOk = PasswordHasher.Verify(request.Password, passwordHash);

            if (found is null || !passwordOk || found.Value.Account.IsDisabled)
            {
                ServerLog.LoginFailed(logger, request.Username);
                return Results.Problem(title: "Invalid username or password.", statusCode: StatusCodes.Status401Unauthorized);
            }

            var account = found.Value.Account;

            // The token is generated here, hashed for storage, and returned to the
            // caller exactly once - it can never be recovered from the database.
            var token = DeviceTokens.Generate();
            var deviceName = DescribeDevice(http);
            await tokens.CreateAsync(account.Id, deviceName, DeviceTokens.Hash(token), cancellationToken);

            ServerLog.LoginSucceeded(logger, account.Username, deviceName);

            return Results.Ok(new LoginResponse(token, account));
        })
        .AllowAnonymous()
        .RequireRateLimiting("login")
        .WithTags("Auth");

        app.MapPost(ApiRoutes.Auth.Logout, async (
            ClaimsPrincipal principal,
            IDeviceTokenStore tokens,
            CancellationToken cancellationToken) =>
        {
            var raw = principal.FindFirstValue(DeviceTokenAuthenticationHandler.TokenIdClaim);
            if (Guid.TryParse(raw, out var tokenId))
            {
                await tokens.RevokeAsync(tokenId, cancellationToken);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithTags("Auth");

        app.MapGet(ApiRoutes.Auth.Me, async (
            IAccountContext context,
            IAccountStore accounts,
            CancellationToken cancellationToken) =>
        {
            var account = await accounts.GetAsync(context.AccountId, cancellationToken);
            return account is null ? Results.NotFound() : Results.Ok(account);
        })
        .RequireAuthorization()
        .WithTags("Auth");

        return app;
    }

    /// <summary>
    /// A human-readable label for the issued token so it can be recognised and
    /// revoked later. Best effort only - never trusted for anything.
    /// </summary>
    private static string DescribeDevice(HttpContext http)
    {
        var agent = http.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(agent))
        {
            return "unknown device";
        }

        return agent.Length <= 100 ? agent : agent[..100];
    }
}
