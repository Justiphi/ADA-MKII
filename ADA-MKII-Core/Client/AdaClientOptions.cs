namespace ADA_MKII_Core.Client;

/// <summary>Configuration for reaching ADA-MKII-Server from a head.</summary>
public sealed class AdaClientOptions
{
    public const string SectionName = "Ada:Client";

    /// <summary>Base address of the server, e.g. https://ada.example.com.</summary>
    public Uri? BaseAddress { get; set; }
}
