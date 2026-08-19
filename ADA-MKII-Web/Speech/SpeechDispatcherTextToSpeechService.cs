using System.Diagnostics;
using ADA_MKII_Core.Abstractions;

namespace ADA_MKII_Web.Speech;

/// <summary>
/// Speech synthesis on Linux through <c>spd-say</c>, the speech-dispatcher client.
///
/// The browser path (<see cref="WebSpeechSynthesisService"/>) plays through
/// whichever machine is *viewing* the page. On a smart mirror that is the same
/// machine, but routing a spoken reply through Chromium to come back out of the
/// Pi's own speakers is a pointless detour - and on a Pi, Chromium's
/// speechSynthesis is backed by speech-dispatcher anyway. This talks to it
/// directly.
///
/// Quality is the honest cost: the default espeak-ng voice is intelligible and
/// robotic. ElevenLabs remains the good-sounding option and remains opt-in and
/// paid, per CLAUDE.md's cost control.
///
/// Requires <c>speech-dispatcher</c>; <see cref="IsSupported"/> is false without it.
/// </summary>
public sealed class SpeechDispatcherTextToSpeechService : ITextToSpeechService, IDisposable
{
    private readonly Lazy<bool> _available = new(() => OperatingSystem.IsLinux() && Which("spd-say"));

    private Process? _speaking;

    public bool IsSupported => _available.Value;

    public async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!IsSupported || text.Trim().Length == 0)
        {
            return;
        }

        await StopAsync();

        var start = new ProcessStartInfo("spd-say") { UseShellExecute = false };

        // Wait for playback to finish, so SpeakAsync completes when the speaking
        // actually ends - the shared UI uses that to leave the Speaking state.
        start.ArgumentList.Add("--wait");

        // Everything after this is data, not options: without it a reply that
        // happens to begin with a hyphen would be parsed as a flag.
        start.ArgumentList.Add("--");
        start.ArgumentList.Add(text);

        var process = Process.Start(start);

        if (process is null)
        {
            return;
        }

        _speaking = process;

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Barge-in: the user started talking. Silence is the correct outcome,
            // not a propagated exception.
            await StopAsync();
        }
        finally
        {
            if (ReferenceEquals(_speaking, process))
            {
                _speaking = null;
            }

            process.Dispose();
        }
    }

    public Task StopAsync()
    {
        if (!IsSupported)
        {
            return Task.CompletedTask;
        }

        var process = _speaking;
        _speaking = null;

        if (process is not null && !process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already gone between the check and the kill.
            }
        }

        // Killing the client does not stop audio already queued in the daemon,
        // so cancel there too.
        try
        {
            using var cancel = Process.Start(new ProcessStartInfo("spd-say")
            {
                ArgumentList = { "--cancel" },
                UseShellExecute = false,
            });

            cancel?.WaitForExit(TimeSpan.FromSeconds(2));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // spd-say vanished from PATH since startup. Nothing useful to do.
        }

        return Task.CompletedTask;
    }

    /// <summary>Whether a command exists on PATH, so IsSupported is honest.</summary>
    private static bool Which(string command)
    {
        try
        {
            using var probe = Process.Start(new ProcessStartInfo("/usr/bin/which")
            {
                ArgumentList = { command },
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });

            if (probe is null)
            {
                return false;
            }

            probe.WaitForExit(TimeSpan.FromSeconds(5));

            return probe.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _speaking?.Dispose();
        _speaking = null;
    }
}
