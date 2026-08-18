using System.Globalization;
using System.Text;
using System.Text.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_API.Tools;

/// <summary>
/// Lets ADA answer questions about the weather.
///
/// This one lives in ADA-MKII-API rather than in Core with the note and calendar
/// tools, and the distinction is the point of the project split: those adapt a
/// Core store, whereas this reaches a third-party service. API is where outbound
/// integrations live.
/// </summary>
public sealed class WeatherTool(IWeatherProvider weather, ISettingsStore settings) : IAssistantTool
{
    public string Name => "get_weather";

    public string Description =>
        "Current conditions and a five-day forecast. Use this for any question about "
        + "the weather, the temperature, or whether it will rain. Omit location to use "
        + "the place the user has configured, which is what they mean by \"here\".";

    public string JsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "location": {
              "type": "string",
              "description": "Place name, e.g. \"Wellington, NZ\". Omit for the user's own location."
            }
          }
        }
        """;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var asked = ReadLocation(arguments);

        // Falling back to the account's setting rather than the server's default,
        // so "what's the weather" means the user's town and not the VPS's.
        var location = asked
            ?? await settings.GetAsync<string>(SettingKeys.WeatherLocation, cancellationToken);

        var forecast = await weather.GetForecastAsync(location, cancellationToken);

        if (forecast is null)
        {
            return ToolResult.Failure(
                asked is null
                    ? "No location is configured, and the default could not be resolved."
                    : $"I could not find anywhere called '{asked}'.");
        }

        var report = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{forecast.Location}: currently {forecast.Current.TemperatureC:0.#}°C")
            .Append(CultureInfo.InvariantCulture, $" ({forecast.Current.Summary}), feels like {forecast.Current.FeelsLikeC:0.#}°C,")
            .Append(CultureInfo.InvariantCulture, $" wind {forecast.Current.WindSpeedKph:0}km/h, humidity {forecast.Current.HumidityPercent}%.");

        if (forecast.Forecast.Count > 0)
        {
            report.AppendLine().Append("Forecast:");

            foreach (var day in forecast.Forecast)
            {
                report.AppendLine().Append(CultureInfo.InvariantCulture,
                    $"- {day.Date:ddd d MMM}: {day.MinC:0.#} to {day.MaxC:0.#}°C, {day.Summary}, {day.PrecipitationChancePercent}% chance of rain");
            }
        }

        return ToolResult.Success(report.ToString());
    }

    /// <summary>
    /// Reads the location argument, tolerating the wrong JSON type the way the
    /// Core tools' shared reader does. Duplicated rather than shared because that
    /// reader is internal to Core, and widening it to public for one caller would
    /// be a worse trade than five lines here.
    /// </summary>
    private static string? ReadLocation(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("location", out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
