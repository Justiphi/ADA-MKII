using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Client;
using ADA_MKII_Core.Notifications;
using ADA_MKII_Core.Speech;
using ADA_MKII_UI_Shared;
using ADA_MKII_UI_Shared.Speech;
using ADA_MKII_Web.Auth;
using ADA_MKII_Web.Components;
using ADA_MKII_Web.Speech;
using Whisper.net.Ggml;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// This head holds no long-lived secret at all: users sign in, and the resulting
// token lives in server memory for the duration of their circuit. It never sees
// the SQL credentials or a provider API key, and the token is never rendered
// into the browser.
var baseAddress = builder.Configuration["Ada:Client:BaseAddress"]
    ?? throw new InvalidOperationException(
        "Ada:Client:BaseAddress is not configured. Point it at ADA-MKII-Server.");

builder.Services.AddSingleton<IServerAddressProvider>(new FixedServerAddressProvider(new Uri(baseAddress)));
builder.Services.AddAdaClient();

// Unattended displays are the exception: a smart mirror has no keyboard to sign
// in with and loses its circuit on every reboot. Given a provisioned device
// token it comes back signed in; without one this stays an ordinary browser
// session that forgets on reload. See KioskSessionStore for the trade.
var kioskToken = builder.Configuration["Ada:Kiosk:DeviceToken"];

if (string.IsNullOrWhiteSpace(kioskToken))
{
    builder.Services.AddScoped<ISessionStore, ScopedSessionStore>();
}
else
{
    builder.Services.AddScoped<ISessionStore>(_ => new KioskSessionStore(kioskToken));
}

builder.Services.AddAdaSharedUi();

// Voice. Two quite different arrangements share one pair of abstractions.
//
// Normally the browser owns the microphone and the speakers, and this head just
// drives them over JS interop - scoped, because the module reference and the
// active recogniser belong to one circuit.
//
// On a device that owns real hardware - a Raspberry Pi behind a smart mirror -
// the browser is the wrong place for both. Its Chromium cannot do speech
// recognition at all, because distribution builds ship without the credentials
// Google's speech service needs, and routing a spoken reply out through the
// browser to reach speakers attached to this very machine is a detour. So the
// audio path skips the browser: ALSA in, speech-dispatcher out, Whisper in
// between. Those are singletons because there is one set of hardware, however
// many circuits are open.
if (builder.Configuration.GetValue("Ada:Speech:OnDevice", defaultValue: false))
{
    builder.Services.AddSingleton(new AudioCaptureOptions(
        builder.Configuration["Ada:Speech:AlsaDevice"] ?? "default"));

    builder.Services.AddSingleton(new WhisperOptions(
        builder.Configuration["Ada:Speech:ModelDirectory"]
            ?? Path.Combine(AppContext.BaseDirectory, "speech"),
        builder.Configuration.GetValue("Ada:Speech:Model", defaultValue: GgmlType.Tiny)));

    builder.Services.AddSingleton<IAudioCapture, ArecordAudioCapture>();
    builder.Services.AddSingleton<ISpeechRecognitionEngine, WhisperRecognitionEngine>();
    builder.Services.AddSingleton<ISpeechToTextService, EngineSpeechToTextService>();
    builder.Services.AddSingleton<ITextToSpeechService, SpeechDispatcherTextToSpeechService>();

    // A wake word only where there is no button to press. Registered here and
    // nowhere else, so every other head keeps push-to-talk and phase 1's
    // reasoning stands untouched.
    if (builder.Configuration.GetValue("Ada:Speech:WakeWord:Enabled", defaultValue: false))
    {
        builder.Services.AddSingleton(new WakeWordOptions(
            builder.Configuration["Ada:Speech:WakeWord:Phrase"] ?? "hey ada",
            builder.Configuration.GetValue("Ada:Speech:WakeWord:WindowSeconds", defaultValue: 3d),
            builder.Configuration.GetValue("Ada:Speech:WakeWord:MinimumLevel", defaultValue: 0.015d)));

        builder.Services.AddSingleton<IWakeWordDetector, WhisperWakeWordDetector>();
    }
}
else
{
    builder.Services.AddScoped<WebSpeechModule>();
    builder.Services.AddScoped<ISpeechToTextService, WebSpeechToTextService>();
    builder.Services.AddScoped<ITextToSpeechService, WebSpeechSynthesisService>();
}

// A browser tab cannot be woken to deliver a reminder, so this head schedules
// nothing. The shared UI still resolves the abstraction and simply skips the
// work - see NullReminderScheduler.
builder.Services.AddSingleton<IReminderScheduler, NullReminderScheduler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // A user-facing error page lands alongside the rest of the UI polish.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Routable pages live in ADA-MKII-UI-Shared, not in this assembly. Without
    // this the server has no endpoint for "/" and every route 404s before
    // Blazor ever renders.
    .AddAdditionalAssemblies(typeof(ADA_MKII_UI_Shared.Routes).Assembly);

app.Run();
