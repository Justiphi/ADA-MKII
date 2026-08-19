namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Listens for a spoken name and reports when it hears one.
///
/// CLAUDE.md rules a wake word out of phase 1, on the grounds of battery drain
/// and false triggers. Half of that reasoning is about a phone: a smart mirror
/// is mains-powered, permanently on, and has no button to press, so waiting to
/// be spoken to is the only interaction it can offer. This exists for that case
/// and is composed only by a head that wants it - nothing registers it by
/// default, and a head with no detector simply never wakes.
///
/// It is an abstraction rather than a concrete Whisper loop because keyword
/// spotting is exactly the thing worth replacing later: a purpose-built detector
/// costs a fraction of the CPU of transcribing everything it hears.
/// </summary>
public interface IWakeWordDetector
{
    /// <summary>False when the microphone or the engine is unavailable here.</summary>
    bool IsSupported { get; }

    /// <summary>The phrase being listened for, for display and for logging.</summary>
    string Phrase { get; }

    /// <summary>
    /// Completes with true when the phrase is heard. Runs until then, so the
    /// caller controls the microphone by cancelling: a detector and a live
    /// recording cannot share one input device.
    /// </summary>
    Task<bool> WaitForWakeWordAsync(CancellationToken cancellationToken);
}
