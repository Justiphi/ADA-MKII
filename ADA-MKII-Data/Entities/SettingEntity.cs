namespace ADA_MKII_Data.Entities;

/// <summary>
/// A non-secret user preference. API keys are deliberately NOT stored here -
/// see CLAUDE.md, "Configuration and secrets".
/// </summary>
public sealed class SettingEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTimeOffset UpdatedUtc { get; set; }
}
