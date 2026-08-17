using Microsoft.JSInterop;

namespace ADA_MKII_Web.Speech;

/// <summary>
/// Lazily imports the speech JS module and caches it for the circuit. Import is
/// deferred rather than done at construction because it needs a live JS runtime,
/// which does not exist during prerendering.
/// </summary>
public sealed class WebSpeechModule(IJSRuntime js) : IAsyncDisposable
{
    private const string ModulePath = "./js/ada-speech.js";

    private IJSObjectReference? _module;

    public async ValueTask<IJSObjectReference> GetAsync(CancellationToken cancellationToken) =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is already gone - there is nothing left to dispose on
            // the other side, and this is the normal path when a tab is closed.
        }

        _module = null;
    }
}
