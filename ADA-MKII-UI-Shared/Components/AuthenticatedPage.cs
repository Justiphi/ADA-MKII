using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Client;
using ADA_MKII_UI_Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ADA_MKII_UI_Shared.Components;

/// <summary>
/// Shared entry behaviour for a page that needs a signed-in account: resolve the
/// session, send an unauthenticated visitor to the login page, and surface an
/// unreachable server as a state rather than an exception.
///
/// Factored out because Chat established this dance and four more pages now need
/// exactly the same one. Getting it subtly different per page is how an offline
/// server turns back into a 500.
/// </summary>
public abstract class AuthenticatedPage : ComponentBase
{
    [Inject]
    protected IAuthClient Auth { get; set; } = default!;

    [Inject]
    protected SessionState Session { get; set; } = default!;

    [Inject]
    protected IServerAddressProvider Server { get; set; } = default!;

    [Inject]
    protected TimeZoneState Zone { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [Inject]
    protected TimeProvider Clock { get; set; } = default!;

    /// <summary>True once the page may render its content.</summary>
    protected bool Ready { get; private set; }

    /// <summary>True when the server did not answer at all.</summary>
    protected bool Unreachable { get; private set; }

    /// <summary>A message to show the user, or null.</summary>
    protected string? Error { get; set; }

    protected override async Task OnInitializedAsync() => await ConnectAsync();

    /// <summary>
    /// Resolves the browser's zone after the first render, then reloads, because
    /// which day counts as "today" depends on it. Interop cannot run any earlier
    /// than this without a JS runtime to call into.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !Ready || Unreachable)
        {
            return;
        }

        bool changed;

        try
        {
            changed = await Zone.EnsureResolvedAsync(CancellationToken.None);
        }
        catch (JSDisconnectedException)
        {
            return;
        }

        if (changed)
        {
            await ReloadAsync();
            StateHasChanged();
        }
    }

    /// <summary>Loads whatever the page shows. Called after the session resolves.</summary>
    protected abstract Task LoadAsync();

    /// <summary>Re-runs <see cref="LoadAsync"/>, mapping the failures every page shares.</summary>
    protected async Task ReloadAsync()
    {
        try
        {
            Error = null;
            await LoadAsync();
        }
        catch (AdaUnreachableException)
        {
            Unreachable = true;
        }
        catch (AdaApiException ex) when (ex.IsAuthFailure)
        {
            // The stored token was revoked or the account was disabled. Signing
            // out locally first stops the page from looping back in on a session
            // the server no longer honours.
            Session.SignedOut();
            Navigation.NavigateTo("/login");
        }
        catch (AdaApiException ex)
        {
            Error = ex.Message;
        }
    }

    protected async Task RetryAsync()
    {
        Unreachable = false;
        Ready = false;
        StateHasChanged();

        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        // A stored token may still be good from a previous run, so ask the server
        // who we are before sending anyone to the login page.
        if (!Session.IsSignedIn)
        {
            try
            {
                var account = await Auth.GetCurrentAsync(CancellationToken.None);

                if (account is null)
                {
                    Navigation.NavigateTo("/login");
                    return;
                }

                Session.SignedIn(account);
            }
            catch (AdaUnreachableException)
            {
                Unreachable = true;
                Ready = true;
                return;
            }
        }

        await ReloadAsync();
        Ready = true;
    }
}
