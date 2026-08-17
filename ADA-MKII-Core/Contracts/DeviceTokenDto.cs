namespace ADA_MKII_Core.Contracts;

/// <summary>
/// A registered client credential. The token itself is never stored - only its
/// hash - so this DTO can be listed safely.
/// </summary>
public sealed record DeviceTokenDto(
    Guid Id,
    Guid AccountId,
    string Name,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? LastSeenUtc,
    DateTimeOffset? RevokedUtc);
