using ADA_MKII_API.Llm;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_API;

/// <summary>Outbound provider composition root. Composed only by ADA-MKII-Server.</summary>
public static class AdaApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbound integrations. When no OpenAI key is configured this
    /// falls back to <see cref="EchoLlmProvider"/> rather than failing at startup,
    /// so the server is runnable and the pipeline testable without a credential.
    /// The chosen provider is reported in the logs on every turn.
    /// </summary>
    public static IServiceCollection AddAdaProviders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(OpenAiOptions.SectionName);
        services.Configure<OpenAiOptions>(section);

        var hasKey = !string.IsNullOrWhiteSpace(section[nameof(OpenAiOptions.ApiKey)]);

        if (hasKey)
        {
            services.AddSingleton<ILlmProvider, OpenAiLlmProvider>();
        }
        else
        {
            services.AddSingleton<ILlmProvider, EchoLlmProvider>();
        }

        return services;
    }
}
