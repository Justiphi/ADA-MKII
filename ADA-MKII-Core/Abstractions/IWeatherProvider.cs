using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Weather for a named place.
///
/// Like the stores, this has two implementations: <c>OpenMeteoWeatherProvider</c>
/// in ADA-MKII-API actually calls the service, and <c>HttpWeatherProvider</c> in
/// Core asks ADA-MKII-Server to do it. A head never talks to a weather service
/// directly - that is the same rule that keeps provider keys off devices, and it
/// holds here even though this particular provider needs no key.
/// </summary>
public interface IWeatherProvider
{
    /// <summary>
    /// Current conditions and a short forecast.
    ///
    /// <paramref name="location"/> is a free-text place name such as
    /// "Tauranga, NZ". Null means "wherever the caller is configured for" - the
    /// account's <c>weather.location</c> setting server-side, and the server's
    /// own default when no account has chosen one.
    ///
    /// Returns null when the place cannot be resolved, which is a normal answer
    /// to a typo rather than an error.
    /// </summary>
    Task<WeatherDto?> GetForecastAsync(string? location, CancellationToken cancellationToken);
}
