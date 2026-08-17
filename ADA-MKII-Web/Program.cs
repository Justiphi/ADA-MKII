using ADA_MKII_Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Phase 4 adds AddAdaClient() and AddAdaSharedUi() here.

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // A user-facing error page lands in phase 4, alongside the real UI.
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
