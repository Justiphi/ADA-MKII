using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_Core;

/// <summary>Core composition root. Composed by ADA-MKII-Server.</summary>
public static class AdaCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the domain services Core owns. Phase 3 adds the assistant
    /// pipeline, tool registry and intent dispatcher here.
    /// </summary>
    public static IServiceCollection AddAdaCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
