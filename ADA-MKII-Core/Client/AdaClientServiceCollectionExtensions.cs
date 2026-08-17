using System.Net.Http.Headers;
using ADA_MKII_Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ADA_MKII_Core.Client;

/// <summary>Client composition root, used by every head.</summary>
public static class AdaClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed HttpClient for ADA-MKII-Server and the HTTP-backed
    /// stores. Heads compose this instead of AddAdaData - they have no database
    /// credential and no provider key, only the token they got at login.
    ///
    /// The head must also register an <see cref="ISessionStore"/>, since where a
    /// token may safely be kept is a platform question.
    /// </summary>
    public static IServiceCollection AddAdaClient(
        this IServiceCollection services,
        Action<AdaClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        // The bearer token is attached inside the clients (see
        // AuthenticatedHttpClient) rather than by a DelegatingHandler, because
        // handlers are built in the handler pool's DI scope and would resolve the
        // wrong ISessionStore on a per-user head.
        services.AddHttpClient<IConversationStore, HttpConversationStore>(ConfigureClient)
            .AddStandardResilienceHandler();

        services.AddHttpClient<ISettingsStore, HttpSettingsStore>(ConfigureClient)
            .AddStandardResilienceHandler();

        services.AddHttpClient<IAuthClient, HttpAuthClient>(ConfigureClient)
            .AddStandardResilienceHandler();

        // No resilience handler on the chat client: its total-request timeout
        // would abort a long streaming turn, and retrying a partially consumed
        // stream would double-charge for tokens already paid for.
        services.AddHttpClient<IAssistantPipeline, HttpAssistantPipeline>(ConfigureClient);

        return services;

        static void ConfigureClient(IServiceProvider provider, HttpClient client)
        {
            var options = provider.GetRequiredService<IOptions<AdaClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress
                ?? throw new InvalidOperationException("AdaClientOptions.BaseAddress is not configured.");
            client.Timeout = Timeout.InfiniteTimeSpan;
        }
    }
}

