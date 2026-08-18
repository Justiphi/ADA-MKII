using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Notifications;

/// <summary>
/// Schedules nothing. Composed by the heads that cannot: a Blazor Server page
/// has no way to wake a closed browser, and Discord is not a device.
///
/// Present so those heads can run the same shared UI without null checks - the
/// pattern already used by NullSpeechToTextService.
/// </summary>
public sealed class NullReminderScheduler : IReminderScheduler
{
    public bool IsSupported => false;

    public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<int> SyncAsync(IReadOnlyList<EventOccurrenceDto> occurrences, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}
