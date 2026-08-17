using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ADA_MKII_Core.Abstractions;
using Microsoft.JSInterop;

namespace ADA_MKII_Web.Speech;

/// <summary>
/// Speech recognition through the browser's Web Speech API.
///
/// The API is callback-driven and this abstraction is a stream, so results are
/// funnelled through a channel: JS calls back into <see cref="OnResult"/>, which
/// writes to the channel that <see cref="ListenAsync"/> is reading.
/// </summary>
public sealed class WebSpeechToTextService(WebSpeechModule module) : ISpeechToTextService, IAsyncDisposable
{
    private Channel<SpeechPartial>? _channel;
    private DotNetObjectReference<WebSpeechToTextService>? _self;

    /// <summary>
    /// Optimistic: the real answer needs JS, which is not callable from a
    /// property. The UI offers the microphone and surfaces a clear error if the
    /// browser turns out not to support it - better than hiding it wrongly during
    /// prerender.
    /// </summary>
    public bool IsSupported => true;

    /// <summary>
    /// The browser prompts for the microphone on first use, so there is nothing
    /// to request up front.
    /// </summary>
    public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public async IAsyncEnumerable<SpeechPartial> ListenAsync(
        CultureInfo culture,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var channel = Channel.CreateUnbounded<SpeechPartial>();
        _channel = channel;
        _self = DotNetObjectReference.Create(this);

        var speech = await module.GetAsync(cancellationToken);
        await speech.InvokeVoidAsync("startRecognition", cancellationToken, _self, culture.Name);

        // Stopping on cancellation has to be explicit: the browser keeps
        // listening otherwise, and the microphone indicator stays on.
        using var registration = cancellationToken.Register(() => _ = StopAsync());

        try
        {
            await foreach (var partial in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return partial;

                if (partial.IsFinal)
                {
                    break;
                }
            }
        }
        finally
        {
            await StopAsync();
        }
    }

    [JSInvokable]
    public void OnResult(string text, bool isFinal) =>
        _channel?.Writer.TryWrite(new SpeechPartial(text, isFinal));

    [JSInvokable]
    public void OnEnded() => _channel?.Writer.TryComplete();

    [JSInvokable]
    public void OnError(string message) =>
        _channel?.Writer.TryComplete(new InvalidOperationException(message));

    private async Task StopAsync()
    {
        _channel?.Writer.TryComplete();
        _channel = null;

        try
        {
            var speech = await module.GetAsync(CancellationToken.None);
            await speech.InvokeVoidAsync("stopRecognition");
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone; the browser tore the recogniser down with the page.
        }

        _self?.Dispose();
        _self = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}
