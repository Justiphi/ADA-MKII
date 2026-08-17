using System.Net;

namespace ADA_MKII_Core.Client;

/// <summary>
/// A non-success response from ADA-MKII-Server. Carries the status so callers can
/// distinguish "your token is bad" from "the server is down" without parsing text.
/// </summary>
public sealed class AdaApiException : Exception
{
    public AdaApiException(HttpStatusCode statusCode, string message)
        : base(message) => StatusCode = statusCode;

    public AdaApiException(HttpStatusCode statusCode, string message, Exception innerException)
        : base(message, innerException) => StatusCode = statusCode;

    public AdaApiException()
    {
    }

    public AdaApiException(string message)
        : base(message)
    {
    }

    public AdaApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>True when the device token is missing, unknown or revoked.</summary>
    public bool IsAuthFailure => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
