using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Non-secret user preferences. Never use this for API keys: the server reads
/// those from environment variables, and a database is the wrong trust root.
/// </summary>
public interface ISettingsStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettingDto>> ListAsync(CancellationToken cancellationToken);
}
