using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_UI.Auth;

/// <summary>
/// The MAUI head's server address, stored per device and editable by the user.
///
/// It has to be editable: the app ships without knowing whether it will talk to a
/// machine on your LAN, a tunnel, or a public domain, and a compiled-in address
/// cannot cover all three. Kept in Preferences rather than SecureStorage because
/// a hostname is not a secret - the bearer token is, and that lives elsewhere.
/// </summary>
public sealed class PreferencesServerAddressProvider : IServerAddressProvider
{
    private const string Key = "ada.serverAddress";

    /// <summary>
    /// Android cannot reach the host's loopback, so 10.0.2.2 is the emulator's
    /// alias for it. This is only a starting point - a physical device must be
    /// pointed somewhere reachable.
    /// </summary>
    private static readonly Uri Fallback =
#if ANDROID
        new("http://10.0.2.2:5100");
#else
        new("http://localhost:5100");
#endif

    private Uri? _cached;

    public Uri BaseAddress
    {
        get
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var stored = Preferences.Default.Get<string?>(Key, null);

            _cached = Uri.TryCreate(stored, UriKind.Absolute, out var parsed) ? parsed : Fallback;
            return _cached;
        }
    }

    public bool CanEdit => true;

    public Task SetAsync(Uri baseAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        Preferences.Default.Set(Key, baseAddress.ToString());
        _cached = baseAddress;

        return Task.CompletedTask;
    }
}
