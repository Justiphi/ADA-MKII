using System.Globalization;

namespace ADA_MKII_Core.Abstractions;

/// <summary>One recognition result. Partials arrive as the user speaks; the last is final.</summary>
public sealed record SpeechPartial(string Text, bool IsFinal);

/// <summary>
/// Speech recognition. Each head supplies its own implementation - the browser's
/// Web Speech API on the web head, platform APIs on MAUI - and the shared UI
/// never learns which.
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// False where the platform cannot listen at all. The UI hides the microphone
    /// rather than offering a button that can only fail.
    /// </summary>
    bool IsSupported { get; }

    Task<bool> RequestPermissionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Listens until the speaker stops or the token is cancelled, yielding partial
    /// results as they firm up.
    /// </summary>
    IAsyncEnumerable<SpeechPartial> ListenAsync(CultureInfo culture, CancellationToken cancellationToken);
}
