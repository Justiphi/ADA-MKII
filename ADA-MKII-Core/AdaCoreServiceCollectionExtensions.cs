using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_Core;

/// <summary>Core composition root. Composed by ADA-MKII-Server.</summary>
public static class AdaCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the assistant pipeline and its options. Phase 3 scope; the tool
    /// registry and intent dispatcher slot in alongside these.
    /// </summary>
    public static IServiceCollection AddAdaCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(TimeProvider.System);

        services.AddOptions<AssistantOptions>()
            .Bind(configuration.GetSection(AssistantOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IAssistantPipeline, AssistantPipeline>();

        return services;
    }
}
