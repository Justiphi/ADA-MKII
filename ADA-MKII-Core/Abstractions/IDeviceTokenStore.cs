using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Storage for client credentials. Only hashes are persisted, so a database dump
/// does not yield usable tokens.
/// </summary>
public interface IDeviceTokenStore
{
    /// <summary>Finds a non-revoked token by its hash. Returns null if unknown or revoked.</summary>
    Task<DeviceTokenDto?> FindActiveAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Records that a token was just used, for staleness auditing.</summary>
    Task TouchAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Registers a token hash if that name is not already present. Returns true if it was created.</summary>
    Task<bool> EnsureAsync(string name, string tokenHash, CancellationToken cancellationToken);
}
