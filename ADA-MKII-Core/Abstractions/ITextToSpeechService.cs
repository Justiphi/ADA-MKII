namespace ADA_MKII_Core.Abstractions;

/// <summary>Where a voice turn currently is. The only voice concept the shared UI models.</summary>
public enum VoiceSessionState
{
    Idle = 0,
    Listening = 1,
    Thinking = 2,
    Speaking = 3,
}

/// <summary>
/// Speech synthesis. On-device by default because it is free and lower latency;
/// a server-side voice is opt-in - see CLAUDE.md on cost control.
/// </summary>
public interface ITextToSpeechService
{
    bool IsSupported { get; }

    /// <summary>Speaks the text, completing when playback finishes or is cancelled.</summary>
    Task SpeakAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Stops immediately. Called before starting a listen so the assistant does not
    /// talk over the user, and hear itself.
    /// </summary>
    Task StopAsync();
}
