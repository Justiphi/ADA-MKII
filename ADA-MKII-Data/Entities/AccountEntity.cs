namespace ADA_MKII_Data.Entities;

/// <summary>
/// A user account. Created only by ADA-MKII-DataManager - there is deliberately
/// no registration endpoint, so the public API has no account-creation surface.
/// </summary>
public sealed class AccountEntity
{
    public Guid Id { get; set; }

    /// <summary>Login name. Stored lowercase so lookups are unambiguous.</summary>
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>PBKDF2, format defined by <c>ADA_MKII_Core.Security.PasswordHasher</c>.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Set to disable the account. Preferred over deletion: it preserves history,
    /// and it immediately invalidates every token already issued.
    /// </summary>
    public DateTimeOffset? DisabledUtc { get; set; }

    public ICollection<ConversationEntity> Conversations { get; } = [];

    public ICollection<DeviceTokenEntity> DeviceTokens { get; } = [];

    public ICollection<SettingEntity> Settings { get; } = [];
}
