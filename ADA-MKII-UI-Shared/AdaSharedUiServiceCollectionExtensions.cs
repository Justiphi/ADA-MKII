using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_UI_Shared;

/// <summary>
/// Shared UI composition root, composed by both heads. Registers only UI-level
/// services: the data and pipeline abstractions come from AddAdaClient, which is
/// what keeps this project free of any knowledge of transport or storage.
/// </summary>
public static class AdaSharedUiServiceCollectionExtensions
{
    public static IServiceCollection AddAdaSharedUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Phase 5 registers the voice session state here. Speech implementations
        // themselves are supplied by each head, never by this project.
        return services;
    }
}
