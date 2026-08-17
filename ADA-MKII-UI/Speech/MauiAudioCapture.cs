using ADA_MKII_Core.Abstractions;
using Plugin.Maui.Audio;

namespace ADA_MKII_UI.Speech;

/// <summary>
/// Microphone capture on MAUI.
///
/// Records 16 kHz mono PCM WAV specifically because that is what Whisper wants;
/// the platform defaults (AAC in an MP4 container on Android) would have to be
/// decoded first. Requesting the right format up front avoids carrying an audio
/// decoder around.
/// </summary>
public sealed class MauiAudioCapture(IAudioManager audioManager) : IAudioCapture
{
    private static readonly AudioRecorderOptions RecordingOptions = new()
    {
        SampleRate = 16_000,
        Channels = ChannelType.Mono,
        BitDepth = BitDepth.Pcm16bit,
        Encoding = Encoding.Wav,

        // Fail loudly if a platform will not give us this format. Silently
        // recording something Whisper cannot read would surface much later as
        // "recognition returns nothing", which is far harder to diagnose.
        ThrowIfNotSupported = true,
    };

    private IAudioRecorder? _recorder;

    public bool IsSupported => true;

    public async Task<bool> RequestPermissionAsync(CancellationToken cancellationToken)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();

        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Microphone>();
        }

        return status == PermissionStatus.Granted;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // A recorder left running from an abandoned attempt would hold the
        // microphone and silently produce nothing here.
        await StopAsync(cancellationToken);

        _recorder = audioManager.CreateRecorder(RecordingOptions);
        await _recorder.StartAsync(RecordingOptions);
    }

    public async Task<Stream?> StopAsync(CancellationToken cancellationToken)
    {
        var recorder = _recorder;
        _recorder = null;

        if (recorder is null || !recorder.IsRecording)
        {
            return null;
        }

        var source = await recorder.StopAsync();
        return source?.GetAudioStream();
    }
}
