namespace ADA_MKII_Core.Contracts;

/// <summary>
/// A user-tweakable, NON-SECRET preference. API keys never live here - they come
/// from environment variables on the server. See CLAUDE.md, "Configuration and secrets".
/// </summary>
public sealed record SettingDto(string Key, string Value);

/// <summary>Well-known <see cref="SettingDto.Key"/> values.</summary>
public static class SettingKeys
{
    public const string Model = "llm.model";
    public const string SystemPrompt = "llm.systemPrompt";
    public const string Temperature = "llm.temperature";
    public const string MaxTokensPerTurn = "llm.maxTokensPerTurn";
    public const string HistoryMessageLimit = "llm.historyMessageLimit";

    /// <summary>
    /// Tokens permitted per calendar month; 0 disables the guard. Tokens rather
    /// than currency: counts are exact and provider-independent, while a price
    /// table goes stale and varies per model.
    /// </summary>
    public const string MonthlyTokenBudget = "cost.monthlyTokenBudget";
    public const string UseElevenLabs = "voice.useElevenLabs";
    public const string VoiceId = "voice.voiceId";
}
