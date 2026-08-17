using Microsoft.Extensions.Logging;

namespace ADA_MKII_Core.Logging;

/// <summary>
/// Source-generated log messages for Core. Note what is absent: no message
/// content is ever logged, only shapes and counts.
/// </summary>
internal static partial class CoreLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Warning,
        Message = "Monthly token budget exhausted: {Used} of {Budget} tokens used. Refusing the turn.")]
    public static partial void BudgetExhausted(ILogger logger, long used, long budget);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Turn complete on {Provider}/{Model}: {TokensIn} in, {TokensOut} out, {HistoryCount} history messages.")]
    public static partial void TurnComplete(
        ILogger logger,
        string provider,
        string model,
        int tokensIn,
        int tokensOut,
        int historyCount);
}
