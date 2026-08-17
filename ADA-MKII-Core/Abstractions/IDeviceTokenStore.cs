using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Storage for client credentials. Only hashes are persisted, so a database dump
/// does not yield usable tokens. Every token belongs to an account - a token is
/// how a device proves which account it is acting as.
/// </summary>
public interface IDeviceTokenStore
{
    /// <summary>
    /// Finds a usable token by its hash. Returns null if the token is unknown,
    /// revoked, or belongs to a disabled account - disabling an account must
    /// immediately invalidate everything already issued to it.
    /// </summary>
    Task<DeviceTokenDto?> FindActiveAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Records that a token was just used, for staleness auditing.</summary>
    Task TouchAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Issues a token to an account. Called on successful login.</summary>
    Task<DeviceTokenDto> CreateAsync(Guid accountId, string name, string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceTokenDto>> ListForAccountAsync(Guid accountId, CancellationToken cancellationToken);

    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken);
}
