using System.Globalization;
using System.Text.Json;

namespace ADA_MKII_Core.Tools;

/// <summary>
/// Lenient readers for model-supplied arguments.
///
/// Everything here tolerates a missing property, a null, or the wrong JSON type,
/// because a model will produce all three and a tool should answer "that was not
/// valid" rather than throw. Types are also coerced across: a model asked for an
/// integer quite often sends the string "30".
/// </summary>
internal static class ToolArgs
{
    public static string? String(JsonElement arguments, string name)
    {
        if (!TryGet(arguments, name, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static int? Int(JsonElement arguments, string name)
    {
        if (!TryGet(arguments, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(String(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    public static bool? Bool(JsonElement arguments, string name)
    {
        if (!TryGet(arguments, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => bool.TryParse(String(arguments, name), out var parsed) ? parsed : null,
        };
    }

    /// <summary>
    /// A local wall-clock date and time, e.g. "2026-08-20T15:00". Any offset the
    /// model attached is discarded: it was told the user's local time and asked
    /// for local times back, and an offset it invented is more likely wrong than
    /// right.
    /// </summary>
    public static DateTime? WallClock(JsonElement arguments, string name)
    {
        var raw = String(arguments, name);

        if (raw is null)
        {
            return null;
        }

        if (DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault,
                out var parsed)
            && parsed != default)
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        }

        return null;
    }

    private static bool TryGet(JsonElement arguments, string name, out JsonElement value)
    {
        value = default;

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!arguments.TryGetProperty(name, out var found) || found.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        value = found;
        return true;
    }
}
