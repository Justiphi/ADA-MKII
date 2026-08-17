namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Where ADA-MKII-Server lives, for this head.
///
/// It cannot come from the settings store like other preferences: you need the
/// server's address before you can ask the server anything. So it is device-local
/// by necessity - and on a phone it must be editable at runtime, because a
/// compiled-in address cannot know your LAN, your tunnel, or your domain.
/// </summary>
public interface IServerAddressProvider
{
    Uri BaseAddress { get; }

    /// <summary>
    /// Whether the user may change it. True on MAUI, where the app ships without
    /// knowing which server it will talk to. False on the web head, which is
    /// deployed alongside a known server and configured by whoever deployed it.
    /// </summary>
    bool CanEdit { get; }

    Task SetAsync(Uri baseAddress, CancellationToken cancellationToken);
}
