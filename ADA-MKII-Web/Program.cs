using ADA_MKII_Core.Client;
using ADA_MKII_UI_Shared;
using ADA_MKII_Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// This head holds exactly one secret: its device token. It never sees the SQL
// credentials or a provider API key, and the token stays server-side - it is
// attached to outbound requests here and never rendered into the browser.
var clientSection = builder.Configuration.GetSection(AdaClientOptions.SectionName);
var baseAddress = clientSection["BaseAddress"]
    ?? throw new InvalidOperationException(
        "Ada:Client:BaseAddress is not configured. Point it at ADA-MKII-Server.");
var deviceToken = clientSection["Token"];

builder.Services.AddAdaClient(options =>
{
    options.BaseAddress = new Uri(baseAddress);
    options.TokenProvider = _ => ValueTask.FromResult(deviceToken);
});

builder.Services.AddAdaSharedUi();

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
