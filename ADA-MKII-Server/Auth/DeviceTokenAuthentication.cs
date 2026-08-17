using System.Security.Claims;
using System.Text.Encodings.Web;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ADA_MKII_Server.Auth;

/// <summary>
/// Bearer-token authentication for a single-user assistant: one long random token
/// per device, stored hashed and revocable by name. Full OAuth/Identity is weeks
/// of work for a user count of one - see CLAUDE.md, "Risks and standing rules".
/// </summary>
public sealed class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDeviceTokenStore tokenStore)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceToken";

    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
        {
            return AuthenticateResult.NoResult();
        }

        var value = header.ToString();
        if (!value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = value[BearerPrefix.Length..].Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.Fail("Empty bearer token.");
        }

        var device = await tokenStore.FindActiveAsync(DeviceTokens.Hash(token), Context.RequestAborted);
        if (device is null)
        {
            // Deliberately vague: do not tell a caller whether the token is
            // unknown or merely revoked.
            return AuthenticateResult.Fail("Invalid bearer token.");
        }

        await tokenStore.TouchAsync(device.Id, Context.RequestAborted);

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
                new Claim(ClaimTypes.Name, device.Name),
            ],
            SchemeName);

        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
