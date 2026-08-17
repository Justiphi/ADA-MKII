namespace ADA_MKII_Core.Client;

/// <summary>
/// The server could not be reached at all - refused, unresolved, or the resilience
/// pipeline gave up.
///
/// It exists so the UI has one thing to catch. Underneath, a dead server can
/// surface as HttpRequestException, TaskCanceledException, or a Polly
/// TimeoutRejectedException depending on which layer gave up first; catching that
/// set correctly in every component is a trap, and missing one turns a routine
/// offline server into an unhandled exception and a 500 page.
/// </summary>
public sealed class AdaUnreachableException : Exception
{
    public AdaUnreachableException(Uri? server, Exception innerException)
        : base($"Could not reach ADA at {server?.Authority ?? "the configured address"}.", innerException) =>
        Server = server;

    public AdaUnreachableException()
    {
    }

    public AdaUnreachableException(string message)
        : base(message)
    {
    }

    public AdaUnreachableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Uri? Server { get; }
}
