using System.Security.Claims;
using System.Text.Encodings.Web;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ADA_MKII_Server.Auth;

/// <summary>
/// Bearer-token authentication. A token is issued at login and identifies both
/// the device and the account it acts as; the account id becomes the principal's
/// NameIdentifier, which is what the data stores scope their queries by.
/// </summary>
public sealed class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDeviceTokenStore tokenStore)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceToken";

    /// <summary>Claim carrying the id of the token itself, so logout can revoke it.</summary>
    public const string TokenIdClaim = "ada:tokenId";

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
            // unknown, revoked, or attached to a disabled account.
            return AuthenticateResult.Fail("Invalid bearer token.");
        }

        await tokenStore.TouchAsync(device.Id, Context.RequestAborted);

        var identity = new ClaimsIdentity(
            [
                // The ACCOUNT id, not the token id - this is what scopes data access.
                new Claim(ClaimTypes.NameIdentifier, device.AccountId.ToString()),
                new Claim(TokenIdClaim, device.Id.ToString()),
                new Claim(ClaimTypes.Name, device.Name),
            ],
            SchemeName);

        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
