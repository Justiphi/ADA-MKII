using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Client;
using ADA_MKII_Core.Speech;
using ADA_MKII_UI.Auth;
using ADA_MKII_UI.Speech;
using ADA_MKII_UI_Shared;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace ADA_MKII_UI;

public static class MauiProgram
{
    /// <summary>
    /// Where ADA-MKII-Server lives. Android cannot reach the host's "localhost",
    /// so 10.0.2.2 is the emulator's alias for it; a real device needs the LAN or
    /// public address instead.
    /// </summary>
    private const string ServerBaseAddress =
#if ANDROID
        "http://10.0.2.2:5100";
#else
        "http://localhost:5100";
#endif

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                fonts.AddFont("FluentSystemIcons-Regular.ttf", "FluentUI");
            });

        builder.Services.AddMauiBlazorWebView();

        // The same UI as the web head, over the same abstractions. This project
        // supplies only what is device-specific.
        builder.Services.AddAdaClient(options => options.BaseAddress = new Uri(ServerBaseAddress));
        builder.Services.AddSingleton<ISessionStore, SecureStorageSessionStore>();
        builder.Services.AddAdaSharedUi();

        // Voice. Synthesis uses MAUI Essentials and works today.
        builder.Services.AddSingleton<ITextToSpeechService, MauiTextToSpeechService>();

        // Recognition is NOT yet implemented on this head. CommunityToolkit.Maui
        // removed its SpeechToText API before the .NET 10 line, so the approach
        // CLAUDE.md assumed is no longer available and a replacement has to be
        // chosen. Registering the null service keeps the UI honest: it hides the
        // microphone rather than offering a button that cannot work.
        builder.Services.AddSingleton<ISpeechToTextService, NullSpeechToTextService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
