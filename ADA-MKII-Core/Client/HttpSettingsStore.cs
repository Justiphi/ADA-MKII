using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Client;

/// <summary>Client-side <see cref="ISettingsStore"/>, mirroring SqlSettingsStore over HTTP.</summary>
public sealed class HttpSettingsStore(HttpClient http, ISessionStore session)
    : AuthenticatedHttpClient(http, session), ISettingsStore
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var response = await SendAsync(HttpMethod.Get, ApiRoutes.Settings.ByKey(key), null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);

        var setting = await response.Content.ReadFromJsonAsync<SettingDto>(cancellationToken);
        return setting is null ? default : Deserialize<T>(setting.Value);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var response = await SendAsync(
            HttpMethod.Put,
            ApiRoutes.Settings.ByKey(key),
            new SetSettingRequest(Serialize(value)),
            cancellationToken);

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<SettingDto>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, ApiRoutes.Settings.Base, null, cancellationToken);
        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<SettingDto>>(cancellationToken) ?? [];
    }

    private static string Serialize<T>(T value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => JsonSerializer.Serialize(value),
    };

    private static T? Deserialize<T>(string raw)
    {
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (target == typeof(string))
        {
            return (T)(object)raw;
        }

        if (target.IsPrimitive || target == typeof(decimal))
        {
            return (T)Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
        }

        return JsonSerializer.Deserialize<T>(raw);
    }
}
