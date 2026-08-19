using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

// ImplicitUsings brings in Microsoft.Maui.Controls, whose View collides with the
// Android one this file is entirely about.
using AndroidView = Android.Views.View;

namespace ADA_MKII_UI;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    // Without this the WebView is never resized for the on-screen keyboard, so
    // the chat composer and its Send button sit behind the IME: you can type,
    // but you cannot see what you typed or reach Send.
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android 15 lays every activity out edge-to-edge, and Android 16 ignores
        // the opt-out entirely, so the BlazorWebView extends underneath the status
        // and navigation bars. That is not merely cosmetic: the shell header ends
        // up inside the status bar's touch region, where taps are swallowed before
        // they reach the page - which made Settings and Sign out impossible to
        // press on a phone.
        //
        // Done natively rather than with CSS env(safe-area-inset-*), because in
        // Android WebView those resolve against the display cutout, not the system
        // bars: on a device without a notch they are simply 0 and the header stays
        // under the clock.
        var content = FindViewById(Android.Resource.Id.Content);

        if (content is not null)
        {
            // The inset padding exposes the window background behind the now
            // transparent system bars; left as the default it renders as white
            // strips above and below a dark app.
            content.SetBackgroundResource(Resource.Color.ada_window_background);

            ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarInsetListener());
        }

        // ADA is dark, so the bar icons have to be light or they vanish against it.
        var window = Window;

        if (window?.DecorView is { } decor
            && WindowCompat.GetInsetsController(window, decor) is { } controller)
        {
            controller.AppearanceLightStatusBars = false;
            controller.AppearanceLightNavigationBars = false;
        }
    }
}

/// <summary>
/// Pads the content view clear of the system bars and of the keyboard.
///
/// The IME is included in the same pass deliberately: under edge-to-edge,
/// <c>adjustResize</c> alone no longer shrinks the window, so the keyboard inset
/// has to be applied here or the composer stays hidden behind it.
/// </summary>
internal sealed class SystemBarInsetListener : Java.Lang.Object, IOnApplyWindowInsetsListener
{
    public WindowInsetsCompat OnApplyWindowInsets(AndroidView? v, WindowInsetsCompat? insets)
    {
        ArgumentNullException.ThrowIfNull(v);
        ArgumentNullException.ThrowIfNull(insets);

        var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.Ime());

        if (bars is not null)
        {
            v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
        }

        return insets;
    }
}
