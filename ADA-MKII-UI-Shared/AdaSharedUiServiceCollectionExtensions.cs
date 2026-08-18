using ADA_MKII_UI_Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ADA_MKII_UI_Shared;

/// <summary>
/// Shared UI composition root, composed by both heads. Registers only UI-level
/// services: the data, auth and pipeline abstractions come from AddAdaClient,
/// which is what keeps this project free of any knowledge of transport or storage.
/// </summary>
public static class AdaSharedUiServiceCollectionExtensions
{
    public static IServiceCollection AddAdaSharedUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped, not singleton: on the web head one Blazor circuit is one user,
        // and a singleton would leak an account across browser sessions.
        services.AddScoped<SessionState>();
        services.AddScoped<VoicePreferences>();

        // Scoped for the same reason: the zone belongs to whoever is looking at
        // this circuit, not to the process.
        services.AddScoped<TimeZoneState>();

        // Pages date things against this rather than DateTimeOffset.UtcNow, per
        // the in-box TimeProvider convention. A head may already have registered
        // one, so do not displace it.
        services.TryAddSingleton(TimeProvider.System);

        // Phase 5 registers voice session state here. Speech implementations
        // themselves are supplied by each head, never by this project.
        return services;
    }
}
