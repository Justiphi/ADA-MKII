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

    // Usernames are logged on auth events because that is what makes a brute-force
    // attempt visible. Passwords and tokens never are.
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Failed login for {Username}.")]
    public static partial void LoginFailed(ILogger logger, string username);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Login succeeded for {Username}; issued a token for {Device}.")]
    public static partial void LoginSucceeded(ILogger logger, string username, string device);
}
