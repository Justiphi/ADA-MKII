namespace ADA_MKII_Core.Client;

/// <summary>Configuration for reaching ADA-MKII-Server from a head.</summary>
public sealed class AdaClientOptions
{
    public const string SectionName = "Ada:Client";

    /// <summary>Base address of the server, e.g. https://ada.example.com.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Supplies the device bearer token. A callback rather than a string so the
    /// MAUI head can read it from SecureStorage lazily, and so a rotated token
    /// takes effect without rebuilding the client.
    /// </summary>
    public Func<CancellationToken, ValueTask<string?>>? TokenProvider { get; set; }
}
