using System.Net;
using System.Net.Http.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Client;

/// <summary>
/// Client-side <see cref="IWeatherProvider"/>: asks ADA-MKII-Server rather than
/// the weather service.
///
/// The indirection is not about secrecy here - Open-Meteo needs no key - but
/// about keeping one rule rather than two. A head calls the server; the server
/// calls the world. Making an exception for the one provider that happens to be
/// keyless is how that rule stops being true.
/// </summary>
public sealed class HttpWeatherProvider(HttpClient http, ISessionStore session)
    : AuthenticatedHttpClient(http, session), IWeatherProvider
{
    public async Task<WeatherDto?> GetForecastAsync(string? location, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            ApiRoutes.Weather.For(location),
            null,
            cancellationToken);

        // The place could not be resolved. Not an error - the caller shows
        // "no weather for that place" rather than a failure.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<WeatherDto>(cancellationToken);
    }
}
