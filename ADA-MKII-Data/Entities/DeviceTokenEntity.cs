namespace ADA_MKII_Data.Entities;

/// <summary>
/// A client credential. Stores only the SHA-256 hash of the token, so this table
/// cannot be used to impersonate a device even if the database is compromised.
/// </summary>
public sealed class DeviceTokenEntity
{
    public Guid Id { get; set; }

    /// <summary>The account this token acts as. Issued at login.</summary>
    public Guid AccountId { get; set; }

    public AccountEntity? Account { get; set; }

    /// <summary>Friendly name so a specific device can be revoked, e.g. "phone".</summary>
    public string Name { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? LastSeenUtc { get; set; }

    public DateTimeOffset? RevokedUtc { get; set; }
}
