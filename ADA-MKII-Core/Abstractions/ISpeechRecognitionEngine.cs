using System.Globalization;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// A swappable speech recognition backend.
///
/// This is the seam that keeps the engine choice replaceable. The UI binds to
/// <see cref="ISpeechToTextService"/> and knows nothing below it; the head binds
/// to this and can change engine without touching either. Whisper runs on-device
/// today; an Azure Speech implementation would slot in here alongside it.
/// </summary>
public interface ISpeechRecognitionEngine
{
    /// <summary>Engine name, for logging and for the settings UI.</summary>
    string Name { get; }

    /// <summary>
    /// True when the engine can run here - the right native binaries for this
    /// architecture, and whatever else it needs.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Whether the engine emits interim results while the user is still speaking.
    /// Whisper does not: it transcribes a finished recording. A streaming engine
    /// such as Azure Speech does, and the UI can show text as it firms up.
    /// </summary>
    bool SupportsInterimResults { get; }

    /// <summary>
    /// Gets the engine ready - downloading a model, warming a connection. Called
    /// before the first transcription so a long one-off cost is not paid inside
    /// what the user experiences as "recognising".
    /// </summary>
    Task<bool> PrepareAsync(IProgress<string>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Transcribes 16 kHz mono PCM WAV audio. Yields interim results first when
    /// the engine produces them, and always ends with a final one.
    /// </summary>
    IAsyncEnumerable<SpeechPartial> TranscribeAsync(
        Stream wavPcm16k,
        CultureInfo culture,
        CancellationToken cancellationToken);
}

/// <summary>
/// Microphone capture, supplied by the head because it is inherently platform
/// work. Produces 16 kHz mono PCM WAV, which is what recognition engines expect.
/// </summary>
public interface IAudioCapture
{
    bool IsSupported { get; }

    Task<bool> RequestPermissionAsync(CancellationToken cancellationToken);

    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops recording and returns the captured audio. Caller owns the stream.</summary>
    Task<Stream?> StopAsync(CancellationToken cancellationToken);
}
