using System.Threading.RateLimiting;
using ADA_MKII_Core;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Security;
using ADA_MKII_Data;
using ADA_MKII_Server.Auth;
using ADA_MKII_Server.Endpoints;
using ADA_MKII_Server.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging. Message bodies and tokens are never logged - see CLAUDE.md.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var connectionString = builder.Configuration.GetConnectionString("Ada")
    ?? throw new InvalidOperationException(
        "Connection string 'Ada' is not configured. Set ConnectionStrings:Ada via user-secrets " +
        "in development or the ConnectionStrings__Ada environment variable in production.");

builder.Services.AddOptions<AdaAuthOptions>()
    .Bind(builder.Configuration.GetSection(AdaAuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// This is the ONLY process that holds the SQL credentials and provider API keys.
builder.Services.AddAdaCore();
builder.Services.AddAdaData(connectionString);

builder.Services.AddAuthentication(DeviceTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(
        DeviceTokenAuthenticationHandler.SchemeName, configureOptions: null);
builder.Services.AddAuthorization();

builder.Services.AddProblemDetails();

// The API is internet-facing by choice, so bound the blast radius of a leaked token.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Unauthenticated on purpose: a health probe must work without a credential.
app.MapGet(ApiRoutes.Health, () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapConversationEndpoints();
app.MapSettingsEndpoints();

await SeedBootstrapTokenAsync(app);

await app.RunAsync();

// Registers the first device token so a client has something to authenticate with.
// Migrations are NOT applied here: run them as a deliberate deploy step.
static async Task SeedBootstrapTokenAsync(WebApplication app)
{
    var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdaAuthOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.BootstrapToken))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>();

    var created = await store.EnsureAsync(
        options.BootstrapTokenName,
        DeviceTokens.Hash(options.BootstrapToken),
        CancellationToken.None);

    if (created)
    {
        ServerLog.BootstrapTokenRegistered(app.Logger, options.BootstrapTokenName);
    }
}

/// <summary>Exposed so integration tests can reference the server's entry point.</summary>
public partial class Program;
