using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Client;
using ADA_MKII_Core.Speech;
using ADA_MKII_UI.Auth;
using ADA_MKII_UI.Speech;
using ADA_MKII_UI_Shared;
using Plugin.Maui.Audio;
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

        // The same UI as the web head, over the same abstractions. This project
        // supplies only what is device-specific.
        builder.Services.AddSingleton<IServerAddressProvider, PreferencesServerAddressProvider>();
        builder.Services.AddAdaClient();
        builder.Services.AddSingleton<ISessionStore, SecureStorageSessionStore>();
        builder.Services.AddAdaSharedUi();

        // Voice. Synthesis uses MAUI Essentials and works today.
        builder.Services.AddSingleton<ITextToSpeechService, MauiTextToSpeechService>();

        // Recognition: microphone + engine, composed by Core. Swapping Whisper for
        // Azure Speech later means changing this one registration - nothing above
        // ISpeechRecognitionEngine knows which engine is in use.
        builder.Services.AddSingleton(AudioManager.Current);
        builder.Services.AddSingleton<IAudioCapture, MauiAudioCapture>();
        builder.Services.AddSingleton<ISpeechRecognitionEngine, WhisperRecognitionEngine>();
        builder.Services.AddSingleton<ISpeechToTextService, EngineSpeechToTextService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
