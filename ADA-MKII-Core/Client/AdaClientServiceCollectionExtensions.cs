using ADA_MKII_Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

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
    public static IServiceCollection AddAdaClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The bearer token is attached inside the clients (see
        // AuthenticatedHttpClient) rather than by a DelegatingHandler, because
        // handlers are built in the handler pool's DI scope and would resolve the
        // wrong ISessionStore on a per-user head.
        services.AddHttpClient<IConversationStore, HttpConversationStore>(ConfigureClient)
            .AddStandardResilienceHandler(ConfigureResilience);

        services.AddHttpClient<ISettingsStore, HttpSettingsStore>(ConfigureClient)
            .AddStandardResilienceHandler(ConfigureResilience);

        services.AddHttpClient<IAuthClient, HttpAuthClient>(ConfigureClient)
            .AddStandardResilienceHandler(ConfigureResilience);

        // No resilience handler on the chat client: its total-request timeout
        // would abort a long streaming turn, and retrying a partially consumed
        // stream would double-charge for tokens already paid for.
        services.AddHttpClient<IAssistantPipeline, HttpAssistantPipeline>(ConfigureClient);

        return services;

        // The defaults are tuned for a service riding out transient faults with
        // nobody watching: 30 seconds total across three retries. Here a person is
        // waiting at a login screen, and a server that is simply down should say
        // so in seconds rather than after half a minute of silence.
        //
        // The constraints are checked at startup: AttemptTimeout must be at most
        // half of TotalRequestTimeout, and CircuitBreaker.SamplingDuration at
        // least twice AttemptTimeout.
        static void ConfigureResilience(Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions options)
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
            options.Retry.MaxRetryAttempts = 2;
        }

        // Runs each time a typed client is constructed, so an address the user
        // changed at runtime takes effect without restarting the app.
        static void ConfigureClient(IServiceProvider provider, HttpClient client)
        {
            client.BaseAddress = provider.GetRequiredService<IServerAddressProvider>().BaseAddress;

            // No client-level timeout: the chat stream is long-lived by design,
            // and per-request deadlines belong to the resilience handlers.
            client.Timeout = Timeout.InfiniteTimeSpan;
        }
    }
}

