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
    public const string MonthlyBudgetUsd = "cost.monthlyBudgetUsd";
    public const string UseElevenLabs = "voice.useElevenLabs";
    public const string VoiceId = "voice.voiceId";
}
