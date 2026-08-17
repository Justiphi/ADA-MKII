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
        try
        {
            var speech = await module.GetAsync(CancellationToken.None);
            await speech.InvokeVoidAsync("stopSpeaking");
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone; the browser stopped playback with the page.
        }
    }
}
