using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_Web.Auth;

/// <summary>
/// The web head's server address, fixed at deployment. Not editable: this head is
/// deployed next to a known server by whoever configured it, and letting a browser
/// visitor repoint it would only invite pointing it somewhere hostile.
/// </summary>
public sealed class FixedServerAddressProvider(Uri baseAddress) : IServerAddressProvider
{
    public Uri BaseAddress { get; } = baseAddress;

    public bool CanEdit => false;

    public Task SetAsync(Uri baseAddress, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The web head's server address is set by configuration.");
}
