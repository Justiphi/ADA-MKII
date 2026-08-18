using System.Threading.RateLimiting;
using ADA_MKII_API;
using ADA_MKII_Core;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Data;
using ADA_MKII_Server.Auth;
using ADA_MKII_Server.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging. Message bodies, passwords and tokens are never logged.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var connectionString = builder.Configuration.GetConnectionString("Ada")
    ?? throw new InvalidOperationException(
        "Connection string 'Ada' is not configured. Set ConnectionStrings:Ada via user-secrets " +
        "in development or the ConnectionStrings__Ada environment variable in production.");

// This is the ONLY process that holds the SQL credentials and provider API keys.
builder.Services.AddAdaCore(builder.Configuration);
builder.Services.AddAdaProviders(builder.Configuration);
builder.Services.AddAdaData(connectionString);

// Lets the account-scoped stores discover who the request belongs to.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAccountContext, HttpAccountContext>();

builder.Services.AddAuthentication(DeviceTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(
        DeviceTokenAuthenticationHandler.SchemeName, configureOptions: null);
builder.Services.AddAuthorization();

// Behind a reverse proxy every request arrives from 127.0.0.1. Without this the
// per-IP rate limiters collapse into a single bucket shared by the whole
// internet - and the login limiter, meant to slow one attacker, would lock out
// everybody after ten attempts from anyone. Only loopback proxies are trusted by
// default, so a forged X-Forwarded-For from outside is ignored.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

builder.Services.AddProblemDetails();

// The API is internet-facing by choice, so bound the blast radius of a leaked
// token - and of password guessing against /api/auth/login.
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

    // Login is far more attractive to brute-force than anything else, so it gets
    // its own much tighter budget on top of the global one.
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

// Must run before anything that reads the client address or scheme.
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Unauthenticated on purpose: a health probe must work without a credential.
app.MapGet(ApiRoutes.Health, () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapAuthEndpoints();
app.MapConversationEndpoints();
app.MapSettingsEndpoints();
app.MapChatEndpoints();
app.MapNoteEndpoints();
app.MapMemoryEndpoints();
app.MapCalendarEndpoints();
app.MapWeatherEndpoints();

// Migrations are NOT applied here: run them as a deliberate deploy step.
// Accounts are NOT seeded here either - ADA-MKII-DataManager is the only way to
// create one, by design.
await app.RunAsync();

/// <summary>Exposed so integration tests can reference the server's entry point.</summary>
public partial class Program;
