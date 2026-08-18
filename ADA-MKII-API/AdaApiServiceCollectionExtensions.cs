using ADA_MKII_API.Llm;
using ADA_MKII_API.Tools;
using ADA_MKII_API.Weather;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_API;

/// <summary>Outbound provider composition root. Composed only by ADA-MKII-Server.</summary>
public static class AdaApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbound integrations.
    ///
    /// All three providers are registered and the choice is made per turn by
    /// <see cref="RoutingLlmProvider"/>, rather than fixed here at startup. That
    /// is what lets a user point a keyless server at their own OpenAI-compatible
    /// endpoint and have it take effect immediately.
    /// </summary>
    public static IServiceCollection AddAdaProviders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));

        services.AddSingleton<OpenAiLlmProvider>();
        services.AddSingleton<EchoLlmProvider>();
        services.AddSingleton<ILlmProvider, RoutingLlmProvider>();

        services.Configure<WeatherOptions>(configuration.GetSection(WeatherOptions.SectionName));

        // A typed client with resilience, per the standing rule for outbound
        // calls. Open-Meteo needs no credential, so there is nothing to withhold
        // here the way OpenAiLlmProvider must withhold its key.
        services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>()
            .AddStandardResilienceHandler();

        // The weather tool belongs here, unlike the note and calendar tools in
        // Core: it is backed by an outbound integration, which is what this
        // project is for.
        services.AddScoped<IAssistantTool, WeatherTool>();

        return services;
    }
}
