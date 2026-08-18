using Microsoft.JSInterop;

namespace ADA_MKII_UI_Shared.Services;

/// <summary>
/// The zone the person looking at the screen is actually in.
///
/// <see cref="TimeZoneInfo.Local"/> is wrong on the web head: that is the zone of
/// the VPS, which is very likely UTC while the user is not. Asking the browser
/// instead gives the right answer on every head, because the MAUI WebView reports
/// the device zone through the same API - so one implementation covers all of them
/// rather than needing a per-head service.
///
/// Until the browser has been asked, <see cref="Zone"/> falls back to
/// <see cref="TimeZoneInfo.Local"/>, which is correct on MAUI and merely a
/// starting point on the web.
/// </summary>
public sealed class TimeZoneState(IJSRuntime js) : IAsyncDisposable
{
    private const string ModulePath = "./_content/ADA-MKII-UI-Shared/js/ada-tz.js";

    private IJSObjectReference? _module;
    private bool _resolved;

    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Local;

    /// <summary>
    /// The id to send to the server with an event, so recurrence expands in the
    /// user's zone rather than the server's. Null means "not resolved", which the
    /// server reads as UTC.
    /// </summary>
    public string? Id => _resolved ? Zone.Id : null;

    /// <summary>
    /// Asks the browser once per circuit.
    ///
    /// **Must only be called from OnAfterRenderAsync.** JS interop during static
    /// prerendering has no JS runtime to reach, which is the failure this project
    /// has already hit once with the speech module.
    /// </summary>
    public async ValueTask<bool> EnsureResolvedAsync(CancellationToken cancellationToken)
    {
        if (_resolved)
        {
            return false;
        }

        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);

        var id = await _module.InvokeAsync<string?>("resolvedTimeZone", cancellationToken);

        _resolved = true;

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            // .NET resolves IANA ids on Windows too, via ICU, so the browser's
            // "Pacific/Auckland" needs no translation table.
            Zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>Converts an instant to the viewer's local wall-clock time.</summary>
    public DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);

    /// <summary>Midnight today, in the viewer's zone, as an absolute instant.</summary>
    public DateTimeOffset TodayStart(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var local = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone);
        var midnight = DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified);

        return new DateTimeOffset(midnight, Zone.GetUtcOffset(midnight));
    }

    public async ValueTask DisposeAsync()
    {
        // Only dispose what was actually imported. Reaching for the module here
        // would import it during teardown, when there may be no JS runtime.
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
            // The circuit is already gone; there is nothing left to release.
        }
    }
}
