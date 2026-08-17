using ADA_MKII_UI_Shared.Services;
using Microsoft.Extensions.DependencyInjection;

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

        // Phase 5 registers voice session state here. Speech implementations
        // themselves are supplied by each head, never by this project.
        return services;
    }
}
