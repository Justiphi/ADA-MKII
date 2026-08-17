using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_UI_Shared.Services;

/// <summary>
/// The user's voice choices, cached for the UI session.
///
/// Settings live server-side per account, so they follow the user between heads
/// rather than being stored per device. Reading them costs a round trip, hence
/// the cache: the chat page consults these on every turn.
/// </summary>
public sealed class VoicePreferences(ISettingsStore settings)
{
    private bool _loaded;

    public bool Enabled { get; private set; } = true;

    public string Provider { get; private set; } = SpeechProviders.Default;

    public bool UseNativeSpeech => Provider == SpeechProviders.Native;

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        // A missing row is normal - it means the user has never changed this, so
        // the documented default stands.
        var enabled = await settings.GetAsync<string>(SettingKeys.VoiceEnabled, cancellationToken);
        var provider = await settings.GetAsync<string>(SettingKeys.SpeechProvider, cancellationToken);

        Enabled = !string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase);
        Provider = provider is SpeechProviders.Server ? SpeechProviders.Server : SpeechProviders.Default;

        _loaded = true;
        Changed?.Invoke();
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        Enabled = enabled;
        await settings.SetAsync(SettingKeys.VoiceEnabled, enabled ? "true" : "false", cancellationToken);
        Changed?.Invoke();
    }

    public async Task SetProviderAsync(string provider, CancellationToken cancellationToken)
    {
        Provider = provider == SpeechProviders.Server ? SpeechProviders.Server : SpeechProviders.Native;
        await settings.SetAsync(SettingKeys.SpeechProvider, Provider, cancellationToken);
        Changed?.Invoke();
    }
}
