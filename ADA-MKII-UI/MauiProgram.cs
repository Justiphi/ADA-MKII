using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace ADA_MKII_UI;

public static class MauiProgram
{
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

        // Phase 4 adds AddAdaClient() and AddAdaSharedUi().
        // Phase 5 adds MauiSpeechToTextService, MauiTextToSpeechService and
        // SecureStorageSecretStore - the device implementations of Core's
        // abstractions. Nothing platform-specific may leak into UI-Shared.

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
