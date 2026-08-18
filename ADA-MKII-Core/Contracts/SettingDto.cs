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

    /// <summary>
    /// OpenAI-compatible endpoint to talk to, e.g. an Ollama server. Blank uses
    /// whatever the server was configured with.
    /// </summary>
    public const string BaseUrl = "llm.baseUrl";
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

    /// <summary>Master switch for voice input and spoken replies.</summary>
    public const string VoiceEnabled = "voice.enabled";

    /// <summary>
    /// Which engine handles speech. See <see cref="SpeechProviders"/>. Defaults to
    /// the platform's own: it is free, lower latency, and keeps audio on the device.
    /// </summary>
    public const string SpeechProvider = "voice.speechProvider";
}

/// <summary>Values for <see cref="SettingKeys.SpeechProvider"/>.</summary>
public static class SpeechProviders
{
    /// <summary>The platform's built-in speech. Default: free, on-device, no key.</summary>
    public const string Native = "native";

    /// <summary>
    /// Route audio through ADA-MKII-Server. Works where a platform has no usable
    /// speech engine, at the cost of sending audio off the device and paying a
    /// provider per minute.
    /// </summary>
    public const string Server = "server";

    public const string Default = Native;
}
