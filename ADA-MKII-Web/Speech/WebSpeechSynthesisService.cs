using ADA_MKII_Core.Abstractions;
using Microsoft.JSInterop;

namespace ADA_MKII_Web.Speech;

/// <summary>
/// Speech synthesis through the browser's speechSynthesis API. Free, on-device
/// and available far more widely than recognition.
/// </summary>
public sealed class WebSpeechSynthesisService(WebSpeechModule module) : ITextToSpeechService
{
    public bool IsSupported => true;

    public async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            var speech = await module.GetAsync(cancellationToken);

            // Returns as soon as the utterance is queued, not when it finishes.
            // The UI treats speaking as fire-and-forget; the user can always
            // interrupt by starting a new listen, which cancels playback.
            await speech.InvokeVoidAsync("speak", cancellationToken, text);
        }
        catch (JSDisconnectedException)
        {
            // The circuit closed mid-utterance; nothing to say and nobody to hear.
        }
    }

    public async Task StopAsync()
    {
        // Nothing can be speaking if the module was never loaded, and importing
        // it here would fail during prerendering.
        if (module.Current is not { } speech)
        {
            return;
        }

        try
        {
            await speech.InvokeVoidAsync("stopSpeaking");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
            // Circuit gone, or no JS runtime available.
        }
    }
}
