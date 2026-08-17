using System.Globalization;
using System.Text.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data.Stores;

/// <summary>
/// Server-side <see cref="ISettingsStore"/>. Values are stored as text; primitives
/// round-trip via invariant culture and everything else via JSON.
/// </summary>
public sealed class SqlSettingsStore(AdaDbContext db, TimeProvider clock) : ISettingsStore
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var raw = await db.Settings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return raw is null ? default : Deserialize<T>(raw);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var raw = Serialize(value);
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (existing is null)
        {
            db.Settings.Add(new SettingEntity { Key = key, Value = raw, UpdatedUtc = clock.GetUtcNow() });
        }
        else
        {
            existing.Value = raw;
            existing.UpdatedUtc = clock.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SettingDto>> ListAsync(CancellationToken cancellationToken)
    {
        var settings = await db.Settings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .Select(s => new SettingDto(s.Key, s.Value))
            .ToListAsync(cancellationToken);

        return settings;
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
