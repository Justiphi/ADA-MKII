using System.Globalization;
using ADA_MKII_Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace ADA_MKII_UI_Shared.Speech;

/// <summary>How the mirror listens for its name.</summary>
/// <param name="Phrase">
/// Matched as a substring of the transcript, lowercased and stripped of
/// punctuation. Short names are the ones Whisper mishears most, so something
/// two syllables or longer triggers far more reliably than "ada" alone.
/// </param>
/// <param name="WindowSeconds">
/// Length of each listening slice. Too short and the name is cut in half at the
/// boundary; too long and the mirror is slow to answer. Slices overlap by no
/// margin at all, which is this design's main weakness - see the class remarks.
/// </param>
/// <param name="MinimumLevel">
/// RMS amplitude, 0 to 1, below which a slice is treated as silence and never
/// transcribed. This is the single most important setting here: it is what stops
/// the Pi transcribing an empty room around the clock.
/// </param>
public sealed record WakeWordOptions(
    string Phrase = "hey ada",
    double WindowSeconds = 3,
    double MinimumLevel = 0.015);

/// <summary>
/// Wake-word detection by transcribing short slices with Whisper and looking for
/// the phrase.
///
/// **This is the honest, dependency-free option, not the good one.** A real
/// keyword spotter - Porcupine, openWakeWord - scores a tiny model against a
/// stream continuously and costs almost nothing. This transcribes whole slices
/// of audio, which on a Raspberry Pi means a Tiny model and a CPU core kept
/// meaningfully busy. It earns its place by needing no key, no account and no
/// extra runtime, exactly as Whisper did for recognition; when the cost becomes
/// the problem, <see cref="IWakeWordDetector"/> is the seam to replace.
///
/// Two known weaknesses, both inherent to slicing rather than streaming:
/// a phrase spoken across a slice boundary is missed, and Whisper invents text
/// when given near-silence - which is what <see cref="WakeWordOptions.MinimumLevel"/>
/// exists to prevent, and why its default is deliberately not zero.
/// </summary>
public sealed class WhisperWakeWordDetector(
    IAudioCapture capture,
    ISpeechRecognitionEngine engine,
    WakeWordOptions options,
    ILogger<WhisperWakeWordDetector> logger) : IWakeWordDetector
{
    /// <summary>Bytes of RIFF header before the samples begin.</summary>
    private const int WavHeaderBytes = 44;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public bool IsSupported => capture.IsSupported && engine.IsSupported;

    public string Phrase => options.Phrase;

    public async Task<bool> WaitForWakeWordAsync(CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            return false;
        }

        if (!await engine.PrepareAsync(progress: null, cancellationToken))
        {
            return false;
        }

        var needle = Normalise(options.Phrase);
        var window = TimeSpan.FromSeconds(options.WindowSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            await capture.StartAsync(cancellationToken);

            Stream? slice;

            try
            {
                await Task.Delay(window, cancellationToken);
            }
            finally
            {
                // Uncancelled, so a cancelled wait still releases the microphone
                // rather than leaving arecord running.
                slice = await capture.StopAsync(CancellationToken.None);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (slice is null)
            {
                continue;
            }

            await using (slice)
            {
                if (!CarriesSpeech(slice))
                {
                    continue;
                }

                slice.Position = 0;

                await foreach (var partial in engine.TranscribeAsync(slice, Culture, cancellationToken))
                {
                    if (!partial.IsFinal)
                    {
                        continue;
                    }

                    if (Normalise(partial.Text).Contains(needle, StringComparison.Ordinal))
                    {
                        WakeLog.Heard(logger, options.Phrase);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a slice is loud enough to be worth transcribing, by RMS amplitude
    /// over its 16-bit samples. Cheap - a pass over the buffer - and it is what
    /// keeps the model idle while the room is quiet.
    /// </summary>
    private bool CarriesSpeech(Stream wav)
    {
        if (wav.Length <= WavHeaderBytes)
        {
            return false;
        }

        wav.Position = WavHeaderBytes;

        var buffer = new byte[8192];
        double sumSquares = 0;
        long samples = 0;
        int read;

        while ((read = wav.Read(buffer, 0, buffer.Length)) > 0)
        {
            // Whole samples only; a trailing odd byte is not one.
            for (var i = 0; i + 1 < read; i += 2)
            {
                double sample = BitConverter.ToInt16(buffer, i) / (double)short.MaxValue;

                sumSquares += sample * sample;
                samples++;
            }
        }

        if (samples == 0)
        {
            return false;
        }

        return Math.Sqrt(sumSquares / samples) >= options.MinimumLevel;
    }

    /// <summary>
    /// Lowercases and reduces anything that is not a letter or digit to a single
    /// space, so "Hey, ADA!" and "hey ada" compare equal.
    /// </summary>
    private static string Normalise(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var space = true;

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                space = false;
            }
            else if (!space)
            {
                builder.Append(' ');
                space = true;
            }
        }

        return builder.ToString().Trim();
    }
}

/// <summary>Source-generated logging, per the CA1848 policy.</summary>
internal static partial class WakeLog
{
    /// <summary>
    /// The phrase is configuration, not speech, so logging it breaks no rule
    /// about message bodies. Nothing else the microphone hears is ever logged.
    /// </summary>
    [LoggerMessage(
        EventId = 7000,
        Level = LogLevel.Information,
        Message = "Heard the wake word {Phrase}.")]
    public static partial void Heard(ILogger logger, string phrase);
}
