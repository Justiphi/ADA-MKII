using System.Globalization;
using System.Runtime.CompilerServices;
using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_Core.Speech;

/// <summary>
/// Turns a microphone plus a recognition engine into the
/// <see cref="ISpeechToTextService"/> the UI consumes.
///
/// It lives in Core rather than in a head because the wiring is the same
/// everywhere: record until the user stops, hand the audio to whichever engine
/// is registered, surface what comes back. Only the two pieces underneath are
/// platform- or vendor-specific.
/// </summary>
public sealed class EngineSpeechToTextService(IAudioCapture capture, ISpeechRecognitionEngine engine)
    : ISpeechToTextService
{
    public bool IsSupported => capture.IsSupported && engine.IsSupported;

    public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken) =>
        capture.RequestPermissionAsync(cancellationToken);

    public async IAsyncEnumerable<SpeechPartial> ListenAsync(
        CultureInfo culture,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(culture);

        // Model download or connection warm-up happens before recording, so the
        // user is not left speaking into something that is not listening yet.
        if (!await engine.PrepareAsync(progress: null, cancellationToken))
        {
            throw new InvalidOperationException($"The {engine.Name} speech engine could not be prepared.");
        }

        await capture.StartAsync(cancellationToken);

        Stream? audio;
        try
        {
            // Record until the caller cancels - the user pressing the mic again,
            // or navigating away. This is push-to-talk, so there is no silence
            // detection to reason about.
            await Task.Delay(Timeout.Infinite, cancellationToken);
            audio = null;
        }
        catch (OperationCanceledException)
        {
            // Expected: cancellation is how recording ends. Stop with an
            // uncancelled token, or stopping would be cancelled too and the
            // audio lost.
            audio = await capture.StopAsync(CancellationToken.None);
        }

        if (audio is null)
        {
            yield break;
        }

        await using (audio)
        {
            // Transcription must not inherit the token that just fired: the user
            // asked to stop *recording*, not to discard what they said.
            await foreach (var partial in engine.TranscribeAsync(audio, culture, CancellationToken.None))
            {
                yield return partial;
            }
        }
    }
}
