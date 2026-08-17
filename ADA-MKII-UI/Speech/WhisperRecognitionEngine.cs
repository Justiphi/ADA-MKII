using System.Globalization;
using System.Runtime.CompilerServices;
using ADA_MKII_Core.Abstractions;
using Whisper.net;
using Whisper.net.Ggml;

namespace ADA_MKII_UI.Speech;

/// <summary>
/// On-device speech recognition with Whisper.net.
///
/// Chosen because nothing else clears the bar: the toolkit's speech API is gone,
/// no MAUI plugin targets .NET 10, and Windows cannot reach
/// <c>Windows.Media.SpeechRecognition</c> while the app is unpackaged. Whisper
/// runs locally, needs no key, and keeps audio on the device - which is the
/// principle the architecture already commits to for speech.
///
/// The trade-off is that it transcribes a finished recording rather than
/// streaming, so <see cref="SupportsInterimResults"/> is false and the user sees
/// their words once, at the end.
/// </summary>
public sealed class WhisperRecognitionEngine : ISpeechRecognitionEngine, IAsyncDisposable
{
    /// <summary>
    /// Base is the smallest model that transcribes ordinary speech acceptably;
    /// Tiny is noticeably worse on anything but clear, close speech. Roughly
    /// 140 MB, downloaded once and cached.
    /// </summary>
    private const GgmlType Model = GgmlType.Base;

    private readonly SemaphoreSlim _prepareLock = new(1, 1);

    private WhisperFactory? _factory;

    public string Name => "whisper";

    /// <summary>
    /// The native binaries ship for arm64 and x86/x64 only. On anything else the
    /// UI must hide the microphone rather than fail at first use.
    /// </summary>
    public bool IsSupported => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
        is System.Runtime.InteropServices.Architecture.X64
        or System.Runtime.InteropServices.Architecture.Arm64
        or System.Runtime.InteropServices.Architecture.X86;

    public bool SupportsInterimResults => false;

    public async Task<bool> PrepareAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (_factory is not null)
        {
            return true;
        }

        await _prepareLock.WaitAsync(cancellationToken);

        try
        {
            if (_factory is not null)
            {
                return true;
            }

            var path = ModelPath;

            if (!File.Exists(path))
            {
                // Downloaded rather than shipped: bundling ~140 MB in the app
                // package would dominate its size for a feature not everyone uses.
                progress?.Report("Downloading the speech model. This happens once.");

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                // Write to a temporary name and move into place, so an interrupted
                // download cannot leave a truncated file that looks valid.
                var partial = path + ".partial";

                await using (var source = await WhisperGgmlDownloader.Default
                    .GetGgmlModelAsync(Model, cancellationToken: cancellationToken))
                await using (var destination = File.Create(partial))
                {
                    await source.CopyToAsync(destination, cancellationToken);
                }

                File.Move(partial, path, overwrite: true);
            }

            _factory = WhisperFactory.FromPath(path);
            return true;
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    public async IAsyncEnumerable<SpeechPartial> TranscribeAsync(
        Stream wavPcm16k,
        CultureInfo culture,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wavPcm16k);
        ArgumentNullException.ThrowIfNull(culture);

        if (_factory is null)
        {
            throw new InvalidOperationException("PrepareAsync must succeed before transcribing.");
        }

        await using var processor = _factory.CreateBuilder()
            .WithLanguage(culture.TwoLetterISOLanguageName)
            .Build();

        // Whisper emits one segment per utterance chunk. They are joined rather
        // than yielded individually: each is a piece of one sentence, not a
        // successively better guess at the whole, so showing them one at a time
        // would look like stuttering rather than progress.
        var text = new System.Text.StringBuilder();

        await foreach (var segment in processor.ProcessAsync(wavPcm16k, cancellationToken))
        {
            text.Append(segment.Text);
        }

        yield return new SpeechPartial(text.ToString().Trim(), IsFinal: true);
    }

    private static string ModelPath => Path.Combine(
        FileSystem.AppDataDirectory,
        "speech",
        $"ggml-{Model.ToString().ToLowerInvariant()}.bin");

    public ValueTask DisposeAsync()
    {
        // WhisperFactory is IDisposable, not IAsyncDisposable - it releases a
        // native handle, which needs no async work.
        _factory?.Dispose();
        _factory = null;

        _prepareLock.Dispose();

        return ValueTask.CompletedTask;
    }
}
