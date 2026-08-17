using System.ComponentModel.DataAnnotations;

namespace ADA_MKII_Server.Auth;

/// <summary>Bound from the "Ada:Auth" configuration section.</summary>
public sealed class AdaAuthOptions
{
    public const string SectionName = "Ada:Auth";

    /// <summary>
    /// Optional. When set, a device token with this value is registered under
    /// <see cref="BootstrapTokenName"/> at startup so the very first client has
    /// something to authenticate with. Supply it via user-secrets in development
    /// and an environment variable in production - never in appsettings.json.
    /// </summary>
    public string? BootstrapToken { get; set; }

    [Required]
    public string BootstrapTokenName { get; set; } = "bootstrap";
}
