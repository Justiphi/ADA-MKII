namespace ADA_MKII_Server.Logging;

/// <summary>
/// Source-generated log messages. The analyzers require these over
/// <c>logger.LogInformation(...)</c> (CA1848), and they keep message templates in
/// one auditable place - which matters because message bodies and tokens must
/// never be logged.
/// </summary>
internal static partial class ServerLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Registered bootstrap device token {Name}.")]
    public static partial void BootstrapTokenRegistered(ILogger logger, string name);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Chat stream failed after the response had begun.")]
    public static partial void ChatStreamFailed(ILogger logger, Exception exception);
}
