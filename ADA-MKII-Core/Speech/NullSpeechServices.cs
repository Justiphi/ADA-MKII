using System.Globalization;
using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_Core.Speech;

/// <summary>
/// Speech recognition for heads that have no microphone - the Discord bot, and
/// any head whose platform support is not yet built. Reports IsSupported = false
/// so the UI hides the control instead of offering one that cannot work.
/// </summary>
public sealed class NullSpeechToTextService : ISpeechToTextService
{
    public bool IsSupported => false;

    public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public async IAsyncEnumerable<SpeechPartial> ListenAsync(
        CultureInfo culture,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}

/// <summary>Speech synthesis for heads that cannot speak. Silently does nothing.</summary>
public sealed class NullTextToSpeechService : ITextToSpeechService
{
    public bool IsSupported => false;

    public Task SpeakAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;
}
