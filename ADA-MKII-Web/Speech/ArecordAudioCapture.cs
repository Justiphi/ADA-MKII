using System.Diagnostics;
using System.Globalization;
using ADA_MKII_Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace ADA_MKII_Web.Speech;

/// <summary>Where the microphone is, for a head that owns real hardware.</summary>
/// <param name="Device">
/// An ALSA device name. <c>default</c> follows the system default; a specific
/// capture device looks like <c>plughw:1,0</c>, which is what a USB microphone
/// usually is on a Raspberry Pi. <c>arecord -l</c> lists them.
/// </param>
/// <param name="MaxSeconds">
/// A hard ceiling on one recording. Without it a stuck listen would fill the
/// disk on a device nobody is looking at.
/// </param>
public sealed record AudioCaptureOptions(string Device = "default", int MaxSeconds = 30);

/// <summary>
/// Microphone capture on Linux by driving <c>arecord</c>.
///
/// This head normally has no microphone of its own - the browser has one, and
/// <see cref="WebSpeechToTextService"/> uses it. A smart mirror inverts that:
/// the Pi runs this process *and* owns the hardware, and its Chromium cannot do
/// speech recognition anyway, because distribution builds ship without the
/// credentials Google's speech service needs. So the audio path skips the
/// browser entirely.
///
/// <c>arecord</c> rather than P/Invoke into libasound: it is part of
/// <c>alsa-utils</c> and present on any Raspberry Pi OS image, it already emits
/// exactly the 16 kHz mono PCM WAV the recognition engines expect, and a process
/// that can be killed is a much simpler thing to reason about than a native
/// capture loop.
///
/// **Linux only.** <see cref="IsSupported"/> is false elsewhere, so the shared UI
/// hides the microphone rather than failing at first use.
/// </summary>
public sealed class ArecordAudioCapture(
    AudioCaptureOptions options,
    ILogger<ArecordAudioCapture> logger) : IAudioCapture, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Process? _recorder;
    private string? _file;

    public bool IsSupported => OperatingSystem.IsLinux();

    /// <summary>
    /// Nothing to ask. On Linux, access to the microphone is a file permission on
    /// the ALSA device, granted by putting the service account in the
    /// <c>audio</c> group - a deployment step, not a runtime prompt.
    /// </summary>
    public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken) =>
        Task.FromResult(IsSupported);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("Microphone capture here requires Linux and alsa-utils.");
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_recorder is not null)
            {
                return;
            }

            // A real file rather than stdout: arecord writes a WAV header with a
            // placeholder length and seeks back to correct it when it stops,
            // which it cannot do through a pipe. Reading the piped form gives a
            // header claiming a size the data does not match.
            _file = Path.Combine(Path.GetTempPath(), $"ada-capture-{Guid.CreateVersion7():N}.wav");

            var start = new ProcessStartInfo("arecord")
            {
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            start.ArgumentList.Add("--device");
            start.ArgumentList.Add(options.Device);
            start.ArgumentList.Add("--format");
            start.ArgumentList.Add("S16_LE");
            start.ArgumentList.Add("--rate");
            start.ArgumentList.Add("16000");
            start.ArgumentList.Add("--channels");
            start.ArgumentList.Add("1");
            start.ArgumentList.Add("--file-type");
            start.ArgumentList.Add("wav");
            start.ArgumentList.Add("--duration");
            start.ArgumentList.Add(options.MaxSeconds.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add(_file);

            _recorder = Process.Start(start)
                ?? throw new InvalidOperationException("arecord did not start. Is alsa-utils installed?");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Stream?> StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        Process? recorder;
        string? file;

        try
        {
            recorder = _recorder;
            file = _file;

            _recorder = null;
            _file = null;
        }
        finally
        {
            _gate.Release();
        }

        if (recorder is null || file is null)
        {
            return null;
        }

        try
        {
            if (!recorder.HasExited)
            {
                // SIGTERM equivalent, not Kill(): arecord traps it, rewrites the
                // WAV header with the true length and exits. Killed outright, the
                // file keeps its placeholder length and decodes as silence.
                recorder.CloseMainWindow();

                if (!recorder.WaitForExit(TimeSpan.FromSeconds(2)))
                {
                    recorder.Kill(entireProcessTree: true);
                    recorder.WaitForExit(TimeSpan.FromSeconds(2));
                }
            }

            if (!File.Exists(file) || new FileInfo(file).Length <= WavHeaderBytes)
            {
                SpeechLog.CaptureEmpty(logger, options.Device);
                return null;
            }

            // Read it out and delete the file here: the caller gets a stream it
            // owns and there is nothing left on disk to clean up later.
            var audio = new MemoryStream(await File.ReadAllBytesAsync(file, cancellationToken));

            return audio;
        }
        finally
        {
            recorder.Dispose();

            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A leftover temp file is not worth failing a transcription over.
            }
        }
    }

    /// <summary>A canonical WAV header. Anything this size or smaller has no audio in it.</summary>
    private const int WavHeaderBytes = 44;

    public void Dispose()
    {
        // A recording still running at shutdown would otherwise outlive the
        // process and keep the microphone open.
        _recorder?.Kill(entireProcessTree: true);
        _recorder?.Dispose();
        _recorder = null;

        _gate.Dispose();
    }
}

/// <summary>Source-generated logging, per the CA1848 policy.</summary>
internal static partial class SpeechLog
{
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Warning,
        Message = "Captured no audio from ALSA device {Device}.")]
    public static partial void CaptureEmpty(ILogger logger, string device);
}
