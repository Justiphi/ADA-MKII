using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_UI.Speech;

/// <summary>
/// On-device speech synthesis via MAUI Essentials. Free and low latency, which is
/// why it is the default and a paid cloud voice is opt-in.
/// </summary>
public sealed class MauiTextToSpeechService : ITextToSpeechService
{
    private CancellationTokenSource? _speaking;

    public bool IsSupported => true;

    public async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Essentials has no Stop(); cancelling the token it was given is the only
        // way to interrupt playback, so the source is kept for StopAsync.
        await StopAsync();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _speaking = cts;

        try
        {
            await TextToSpeech.Default.SpeakAsync(text, options: null, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Interrupted, most likely by the user starting a new listen.
        }
        finally
        {
            if (ReferenceEquals(_speaking, cts))
            {
                _speaking = null;
            }

            cts.Dispose();
        }
    }

    public Task StopAsync()
    {
        var cts = _speaking;
        _speaking = null;

        if (cts is not null && !cts.IsCancellationRequested)
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }
}
