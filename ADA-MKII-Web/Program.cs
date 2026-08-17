using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Client;
using ADA_MKII_UI_Shared;
using ADA_MKII_Web.Auth;
using ADA_MKII_Web.Components;
using ADA_MKII_Web.Speech;

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
builder.Services.AddScoped<ISessionStore, ScopedSessionStore>();
builder.Services.AddAdaSharedUi();

// Voice, supplied by this head. Scoped because the JS module reference and the
// active recogniser belong to one circuit.
builder.Services.AddScoped<WebSpeechModule>();
builder.Services.AddScoped<ISpeechToTextService, WebSpeechToTextService>();
builder.Services.AddScoped<ITextToSpeechService, WebSpeechSynthesisService>();

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
