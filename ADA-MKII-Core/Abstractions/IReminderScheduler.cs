using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Hands upcoming reminders to the device, so they fire when the app is closed.
///
/// The scheduling is the platform's, not ours: on Android these become
/// AlarmManager entries the OS owns and delivers whether or not ADA is running
/// or online. That is why this needs no local database - see
/// <c>ReminderSync</c> for what that does and does not buy.
/// </summary>
public interface IReminderScheduler
{
    /// <summary>
    /// False where the platform cannot schedule anything - the web head, Discord.
    /// Callers should skip the fetch entirely rather than do work for nobody.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Asks for notification permission if it is not already granted. Android 13+
    /// requires this at runtime; returns false if the user declines, which is a
    /// normal outcome and not an error.
    /// </summary>
    Task<bool> RequestPermissionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces every pending reminder with the ones derived from
    /// <paramref name="occurrences"/>, and returns how many were scheduled.
    ///
    /// Replace rather than merge: occurrences have no stable identity across a
    /// series edit, so reconciling them individually would leave orphans behind
    /// for any event that moved or was cancelled.
    /// </summary>
    Task<int> SyncAsync(IReadOnlyList<EventOccurrenceDto> occurrences, CancellationToken cancellationToken);
}
