namespace ADA_MKII_Core.Contracts;

/// <summary>Conditions right now.</summary>
public sealed record CurrentWeatherDto(
    double TemperatureC,
    double FeelsLikeC,
    int HumidityPercent,
    double WindSpeedKph,
    double PrecipitationMm,
    int WeatherCode)
{
    /// <summary>Plain-English conditions, derived from <see cref="WeatherCode"/>.</summary>
    public string Summary => WeatherCodes.Describe(WeatherCode);
}

/// <summary>One day of the forecast.</summary>
public sealed record DailyForecastDto(
    DateOnly Date,
    double MinC,
    double MaxC,
    int PrecipitationChancePercent,
    int WeatherCode)
{
    public string Summary => WeatherCodes.Describe(WeatherCode);
}

/// <summary>
/// Current conditions plus a short forecast for one place.
///
/// <see cref="Location"/> is what the provider resolved, not what was asked for -
/// "Tauranga, NZ" comes back as "Tauranga, New Zealand", which is worth showing
/// so a wrong match is visible rather than silent.
/// </summary>
public sealed record WeatherDto(
    string Location,
    CurrentWeatherDto Current,
    IReadOnlyList<DailyForecastDto> Forecast,
    DateTimeOffset RetrievedUtc);

/// <summary>
/// WMO weather interpretation codes, which is what Open-Meteo reports instead of
/// prose.
///
/// Kept in Core rather than in the provider because both the dashboard and the
/// weather tool need the same words - if they diverged, ADA would describe the
/// weather differently from the screen the user is looking at.
/// </summary>
public static class WeatherCodes
{
    public static string Describe(int code) => code switch
    {
        0 => "clear",
        1 => "mainly clear",
        2 => "partly cloudy",
        3 => "overcast",
        45 or 48 => "fog",
        51 => "light drizzle",
        53 => "drizzle",
        55 => "heavy drizzle",
        56 or 57 => "freezing drizzle",
        61 => "light rain",
        63 => "rain",
        65 => "heavy rain",
        66 or 67 => "freezing rain",
        71 => "light snow",
        73 => "snow",
        75 => "heavy snow",
        77 => "snow grains",
        80 => "light showers",
        81 => "showers",
        82 => "violent showers",
        85 or 86 => "snow showers",
        95 => "thunderstorm",
        96 or 99 => "thunderstorm with hail",
        _ => "unknown",
    };

    /// <summary>
    /// An emoji for the same code. The dashboard uses this instead of shipping an
    /// icon set: it costs nothing, scales with the font, and needs no licence.
    /// </summary>
    public static string Icon(int code) => code switch
    {
        0 or 1 => "☀️",
        2 => "⛅",
        3 => "☁️",
        45 or 48 => "\U0001f32b️",
        >= 51 and <= 57 => "\U0001f327️",
        >= 61 and <= 67 => "\U0001f327️",
        >= 71 and <= 77 => "\U0001f328️",
        >= 80 and <= 82 => "\U0001f326️",
        85 or 86 => "\U0001f328️",
        >= 95 => "⛈️",
        _ => "\U0001f321️",
    };
}
