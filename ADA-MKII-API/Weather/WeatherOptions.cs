namespace ADA_MKII_API.Weather;

/// <summary>
/// Bound from "Ada:Weather".
///
/// Note the absence of an API key. Open-Meteo is free for non-commercial use and
/// requires no credential, which is why it was chosen: a weather provider that
/// needed a key would add a second secret to the deployment for something the
/// user could look up on their phone.
/// </summary>
public sealed class WeatherOptions
{
    public const string SectionName = "Ada:Weather";

    /// <summary>
    /// Where to report weather for when the account has not chosen. A per-account
    /// <c>weather.location</c> setting overrides it.
    /// </summary>
    public string DefaultLocation { get; set; } = "Tauranga, NZ";

    /// <summary>Days of forecast to fetch, including today.</summary>
    public int ForecastDays { get; set; } = 5;

    /// <summary>
    /// How long a fetched forecast is reused. Weather does not change minute to
    /// minute, and every dashboard load would otherwise be an outbound call.
    /// </summary>
    public TimeSpan CacheFor { get; set; } = TimeSpan.FromMinutes(15);

    public string ForecastBaseUrl { get; set; } = "https://api.open-meteo.com";

    public string GeocodingBaseUrl { get; set; } = "https://geocoding-api.open-meteo.com";
}
