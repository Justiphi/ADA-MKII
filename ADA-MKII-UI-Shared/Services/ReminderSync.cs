using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Client;

namespace ADA_MKII_UI_Shared.Services;

/// <summary>
/// Pushes the next few weeks of reminders down to the device.
///
/// **This is not the offline cache CLAUDE.md rejected, and deliberately so.**
/// Nothing is stored by ADA. The occurrences are handed to the platform's own
/// scheduler, which owns them from then on and fires them with the app closed
/// and the phone offline. There is no local copy to go stale, no write path, and
/// therefore no outbox and no conflict rules - which were the actual reasons a
/// cache was ruled out.
///
/// What it costs instead: reminders only exist as far ahead as the last
/// successful sync reached. Leave the app unopened for longer than
/// <see cref="HorizonDays"/> and they run out. Server-side push is what removes
/// that, and remains the upgrade path.
/// </summary>
public sealed class ReminderSync(
    ICalendarStore calendar,
    IReminderScheduler scheduler,
    TimeProvider clock)
{
    /// <summary>
    /// How far ahead to schedule. Long enough to survive a fortnight of not
    /// opening the app; short enough to stay well inside Android's ceiling on
    /// pending alarms, which a daily series would otherwise eat into.
    /// </summary>
    public const int HorizonDays = 30;

    /// <summary>
    /// How long a successful sync is trusted for. The horizon is 30 days, so
    /// re-fetching more often than this buys nothing but round trips.
    /// </summary>
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(6);

    /// <summary>When the last sync succeeded, or null if it never has.</summary>
    public DateTimeOffset? LastSyncedUtc { get; private set; }

    /// <summary>How many reminders the device is currently holding, as of that sync.</summary>
    public int ScheduledCount { get; private set; }

    /// <summary>
    /// Syncs only if the last one is stale. Called from the layout, which renders
    /// on every navigation - without the throttle, moving between pages would
    /// re-fetch the calendar each time.
    /// </summary>
    public Task<bool> SyncIfStaleAsync(CancellationToken cancellationToken)
    {
        if (LastSyncedUtc is { } last && clock.GetUtcNow() - last < MinimumInterval)
        {
            return Task.FromResult(false);
        }

        return SyncAsync(cancellationToken);
    }

    /// <summary>
    /// Fetches upcoming occurrences and replaces the device's pending reminders.
    ///
    /// Never throws: this runs on app resume, where an unreachable server is
    /// ordinary and must leave the already-scheduled reminders alone rather than
    /// clearing them. Returns false when nothing was changed.
    /// </summary>
    public async Task<bool> SyncAsync(CancellationToken cancellationToken)
    {
        if (!scheduler.IsSupported)
        {
            return false;
        }

        var granted = await scheduler.RequestPermissionAsync(cancellationToken);

        if (!granted)
        {
            return false;
        }

        var now = clock.GetUtcNow();

        try
        {
            var occurrences = await calendar.ListOccurrencesAsync(
                now,
                now.AddDays(HorizonDays),
                cancellationToken);

            // Only what the user actually asked to be reminded about, and only
            // what is still in the future - the window starts at "now", so its
            // first occurrence may already have begun.
            var due = occurrences
                .Where(o => o.ReminderEnabled && o.StartsUtc > now)
                .ToList();

            ScheduledCount = await scheduler.SyncAsync(due, cancellationToken);
            LastSyncedUtc = now;

            return true;
        }
        catch (AdaUnreachableException)
        {
            // Offline. Whatever the device already holds stays scheduled, which
            // is the entire point of handing them over in the first place.
            return false;
        }
        catch (AdaApiException)
        {
            // Signed out, or the server refused. Same reasoning: do not clear.
            return false;
        }
    }
}
