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

