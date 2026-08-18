using ADA_MKII_Core;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Server.Endpoints;

/// <summary>
/// Weather, fetched by the server on a client's behalf.
///
/// Authorised like everything else: the response is cheap, but an open endpoint
/// is an open proxy to a third-party service, and this one takes a caller-supplied
/// place name.
/// </summary>
public static class WeatherEndpoints
{
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ApiRoutes.Weather.Base, async (
                IWeatherProvider weather,
                ISettingsStore settings,
                CancellationToken ct,
                string? location = null) =>
            {
                // An explicit location wins; otherwise the account's own setting;
                // otherwise the provider falls back to the server default.
                var resolved = string.IsNullOrWhiteSpace(location)
                    ? await settings.GetAsync<string>(SettingKeys.WeatherLocation, ct)
                    : location;

                var forecast = await weather.GetForecastAsync(resolved, ct);

                return forecast is null ? Results.NotFound() : Results.Ok(forecast);
            })
            .RequireAuthorization()
            .WithTags("Weather");

        return app;
    }
}
