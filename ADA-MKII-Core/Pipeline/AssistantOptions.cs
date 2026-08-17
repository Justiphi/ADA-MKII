using System.ComponentModel.DataAnnotations;

namespace ADA_MKII_Core.Pipeline;

/// <summary>
/// Defaults for a conversational turn, bound from "Ada:Assistant". Every value
/// here can be overridden at runtime by a row in the Settings table, so the
/// assistant can be retuned without a redeploy - see <c>SettingKeys</c>.
/// </summary>
public sealed class AssistantOptions
{
    public const string SectionName = "Ada:Assistant";

    [Required]
    public string Model { get; set; } = "gpt-4o-mini";

    [Required]
    public string SystemPrompt { get; set; } =
        "You are ADA, a concise and practical personal assistant. Answer directly and avoid filler.";

    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.7;

    /// <summary>Hard ceiling on completion length. The primary per-turn cost control.</summary>
    [Range(1, 32_000)]
    public int MaxOutputTokens { get; set; } = 800;

    /// <summary>
    /// How many prior messages to replay as context. Unbounded history means the
    /// prompt - and the bill - grows without limit as a conversation ages.
    /// </summary>
    [Range(0, 500)]
    public int HistoryMessageLimit { get; set; } = 20;

    /// <summary>
    /// Total tokens permitted per calendar month; 0 disables the guard. Expressed
    /// in tokens rather than currency deliberately: token counts are exact and
    /// provider-independent, whereas a hardcoded price table silently goes stale
    /// and differs per model.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long MonthlyTokenBudget { get; set; } = 2_000_000;
}
