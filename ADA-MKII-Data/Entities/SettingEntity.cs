namespace ADA_MKII_Data.Entities;

/// <summary>
/// A non-secret user preference, owned by an account. Settings are per-user only:
/// system-wide defaults come from configuration (Ada:Assistant), so there is no
/// global-versus-user fallback chain in the database to reason about.
///
/// API keys are deliberately NOT stored here - see CLAUDE.md, "Configuration and secrets".
/// </summary>
public sealed class SettingEntity
{
    public Guid AccountId { get; set; }

    public AccountEntity? Account { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTimeOffset UpdatedUtc { get; set; }
}
