using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using Microsoft.Extensions.Options;

namespace ADA_MKII_API.Weather;

/// <summary>
/// Weather from Open-Meteo. Two calls: geocode the place name, then fetch the
/// forecast for those coordinates.
///
/// Chosen because it needs no API key, so a fresh deployment reports weather
/// before anyone has configured a credential - the same property that makes
/// EchoLlmProvider useful. Nothing here is a secret, but the call still happens
/// server-side, because a head that reaches a third-party service directly is
/// the pattern this architecture exists to prevent.
/// </summary>
public sealed class OpenMeteoWeatherProvider(HttpClient http, IOptions<WeatherOptions> options) : IWeatherProvider
{
    private readonly WeatherOptions _options = options.Value;

    /// <summary>
    /// Resolved places and their forecasts, keyed by the location as asked for.
    ///
    /// A plain dictionary rather than IMemoryCache: this is one small entry per
    /// distinct place a household asks about, and adding a caching package for
    /// that would be heavier than the thing being cached.
    /// </summary>
    private readonly ConcurrentDictionary<string, (DateTimeOffset FetchedUtc, WeatherDto Weather)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<WeatherDto?> GetForecastAsync(string? location, CancellationToken cancellationToken)
    {
        var wanted = string.IsNullOrWhiteSpace(location) ? _options.DefaultLocation : location.Trim();

        if (_cache.TryGetValue(wanted, out var cached)
            && DateTimeOffset.UtcNow - cached.FetchedUtc < _options.CacheFor)
        {
            return cached.Weather;
        }

        var place = await GeocodeAsync(wanted, cancellationToken);

        if (place is null)
        {
            return null;
        }

        var forecast = await FetchForecastAsync(place, cancellationToken);

        if (forecast is not null)
        {
            _cache[wanted] = (DateTimeOffset.UtcNow, forecast);
        }

        return forecast;
    }

    /// <summary>
    /// Turns "Tauranga, NZ" into coordinates.
    ///
    /// The country half matters: searching for "Tauranga" alone returns a place
    /// in Sudan as its second hit, so anything after the comma is used to filter
    /// by country code or name rather than being thrown away.
    /// </summary>
    private async Task<GeocodedPlace?> GeocodeAsync(string location, CancellationToken cancellationToken)
    {
        var comma = location.IndexOf(',', StringComparison.Ordinal);

        var name = comma < 0 ? location : location[..comma].Trim();
        var country = comma < 0 ? null : location[(comma + 1)..].Trim();

        if (name.Length == 0)
        {
            return null;
        }

        var uri = new Uri(
            $"{_options.GeocodingBaseUrl}/v1/search?name={Uri.EscapeDataString(name)}&count=10&language=en&format=json");

        var response = await http.GetFromJsonAsync<GeocodingResponse>(uri, cancellationToken);

        if (response?.Results is not { Count: > 0 } results)
        {
            return null;
        }

        var match = country is null
            ? results[0]
            : results.FirstOrDefault(r =>
                  string.Equals(r.CountryCode, country, StringComparison.OrdinalIgnoreCase)
                  || string.Equals(r.Country, country, StringComparison.OrdinalIgnoreCase))
              // A country that matched nothing is more likely a typo than a
              // reason to report the weather somewhere else entirely.
              ?? results[0];

        return new GeocodedPlace(
            match.Name,
            match.Country,
            match.Latitude,
            match.Longitude,
            match.Timezone ?? "UTC");
    }

    private async Task<WeatherDto?> FetchForecastAsync(GeocodedPlace place, CancellationToken cancellationToken)
    {
        var days = Math.Clamp(_options.ForecastDays, 1, 16);

        // Coordinates formatted invariantly and up front: a decimal comma from a
        // European server locale would silently split the query parameter.
        var latitude = place.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = place.Longitude.ToString(CultureInfo.InvariantCulture);

        var uri = new Uri(
            $"{_options.ForecastBaseUrl}/v1/forecast"
            + $"?latitude={latitude}&longitude={longitude}"
            + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,weather_code,wind_speed_10m"
            + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max"
            + $"&timezone={Uri.EscapeDataString(place.TimeZoneId)}&forecast_days={days}");

        var response = await http.GetFromJsonAsync<ForecastResponse>(uri, cancellationToken);

        if (response?.Current is not { } current || response.Daily is not { } daily)
        {
            return null;
        }

        var forecast = new List<DailyForecastDto>();

        for (var i = 0; i < daily.Time.Count; i++)
        {
            if (!DateOnly.TryParse(daily.Time[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            forecast.Add(new DailyForecastDto(
                date,
                At(daily.TemperatureMin, i),
                At(daily.TemperatureMax, i),
                (int)At(daily.PrecipitationProbabilityMax, i),
                (int)At(daily.WeatherCode, i)));
        }

        var label = string.IsNullOrWhiteSpace(place.Country) ? place.Name : $"{place.Name}, {place.Country}";

        return new WeatherDto(
            label,
            new CurrentWeatherDto(
                current.Temperature,
                current.ApparentTemperature,
                (int)current.RelativeHumidity,
                current.WindSpeed,
                current.Precipitation,
                (int)current.WeatherCode),
            forecast,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Open-Meteo returns parallel arrays, and a null slot is possible for a day
    /// it has no value for. Treated as zero rather than skipping the day, so the
    /// arrays stay aligned with their dates.
    /// </summary>
    private static double At(List<double?>? values, int index) =>
        values is not null && index < values.Count ? values[index] ?? 0 : 0;

    private sealed record GeocodedPlace(
        string Name,
        string? Country,
        double Latitude,
        double Longitude,
        string TimeZoneId);

    private sealed class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult>? Results { get; set; }
    }

    private sealed class GeocodingResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }
    }

    private sealed class ForecastResponse
    {
        [JsonPropertyName("current")]
        public CurrentBlock? Current { get; set; }

        [JsonPropertyName("daily")]
        public DailyBlock? Daily { get; set; }
    }

    private sealed class CurrentBlock
    {
        [JsonPropertyName("temperature_2m")]
        public double Temperature { get; set; }

        [JsonPropertyName("apparent_temperature")]
        public double ApparentTemperature { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public double RelativeHumidity { get; set; }

        [JsonPropertyName("precipitation")]
        public double Precipitation { get; set; }

        [JsonPropertyName("weather_code")]
        public double WeatherCode { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double WindSpeed { get; set; }
    }

    private sealed class DailyBlock
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = [];

        [JsonPropertyName("weather_code")]
        public List<double?>? WeatherCode { get; set; }

        [JsonPropertyName("temperature_2m_max")]
        public List<double?>? TemperatureMax { get; set; }

        [JsonPropertyName("temperature_2m_min")]
        public List<double?>? TemperatureMin { get; set; }

        [JsonPropertyName("precipitation_probability_max")]
        public List<double?>? PrecipitationProbabilityMax { get; set; }
    }
}
