using System.Security.Claims;
using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_Server.Auth;

/// <summary>
/// Resolves the current account from the authenticated principal, so the data
/// stores can scope every query without any endpoint having to remember to pass
/// an owner id.
/// </summary>
public sealed class HttpAccountContext(IHttpContextAccessor accessor) : IAccountContext
{
    public bool IsAuthenticated => TryGetAccountId(out _);

    public Guid AccountId => TryGetAccountId(out var id)
        ? id
        // Reaching a scoped store without an authenticated principal is a wiring
        // bug, not a user error: failing loudly here beats silently querying with
        // Guid.Empty and returning nothing.
        : throw new InvalidOperationException(
            "No authenticated account on this request. An account-scoped store was resolved outside an authorised endpoint.");

    private bool TryGetAccountId(out Guid accountId)
    {
        accountId = default;

        var value = accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return value is not null && Guid.TryParse(value, out accountId);
    }
}
