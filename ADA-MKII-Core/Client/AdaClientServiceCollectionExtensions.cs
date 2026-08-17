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
    /// credential and no provider key, only a device token.
    /// </summary>
    public static IServiceCollection AddAdaClient(
        this IServiceCollection services,
        Action<AdaClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        services.AddTransient<AdaAuthHandler>();

        services.AddHttpClient<IConversationStore, HttpConversationStore>(ConfigureClient)
            .AddHttpMessageHandler<AdaAuthHandler>()
            .AddStandardResilienceHandler();

        services.AddHttpClient<ISettingsStore, HttpSettingsStore>(ConfigureClient)
            .AddHttpMessageHandler<AdaAuthHandler>()
            .AddStandardResilienceHandler();

        // No resilience handler on the chat client: its total-request timeout
        // would abort a long streaming turn, and retrying a partially consumed
        // stream would double-charge for tokens already paid for.
        services.AddHttpClient<IAssistantPipeline, HttpAssistantPipeline>(ConfigureClient)
            .AddHttpMessageHandler<AdaAuthHandler>();

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

/// <summary>
/// Attaches the device bearer token. A delegating handler rather than a header
/// set at construction, so the token is read per request and a rotation takes
/// effect without rebuilding the client.
/// </summary>
internal sealed class AdaAuthHandler(IOptions<AdaClientOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = options.Value.TokenProvider;
        if (provider is not null)
        {
            var token = await provider(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
