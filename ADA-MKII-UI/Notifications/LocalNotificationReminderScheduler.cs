using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace ADA_MKII_UI.Notifications;

/// <summary>
/// Device reminders via Plugin.LocalNotification.
///
/// Once handed over, the OS owns these: they fire with ADA closed and the phone
/// offline, which is the whole reason reminders are scheduled locally rather
/// than pushed. See <c>ReminderSync</c> for why that needs no cache of our own.
///
/// **Known limit: alarms do not survive a reboot.** The package ships no
/// BOOT_COMPLETED receiver, and Android clears AlarmManager entries when the
/// device restarts. Reminders resume as soon as the app is next opened. Fixing
/// it properly means a boot receiver that re-schedules from a stored copy - and
/// that *would* be the local cache this design has so far avoided, so it is a
/// deliberate trade rather than an oversight.
/// </summary>
public sealed class LocalNotificationReminderScheduler(ILogger<LocalNotificationReminderScheduler> logger)
    : IReminderScheduler
{
    /// <summary>
    /// A ceiling on pending alarms. Android starts dropping them well before
    /// this, and a daily series over the sync horizon is the realistic way to
    /// approach it.
    /// </summary>
    private const int MaxPending = 200;

    public bool IsSupported => LocalNotificationCenter.Current.IsSupported;

    public async Task<bool> RequestPermissionAsync(CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            return false;
        }

        var permission = new NotificationPermission { AskPermission = true };

        // Already granted is the common case, and re-asking would be rude; the
        // package returns true without prompting when it can.
        return await LocalNotificationCenter.Current.AreNotificationsEnabled(permission)
            || await LocalNotificationCenter.Current.RequestNotificationPermission(permission);
    }

    public async Task<int> SyncAsync(
        IReadOnlyList<EventOccurrenceDto> occurrences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(occurrences);

        if (!IsSupported)
        {
            return 0;
        }

        // Cleared and rebuilt wholesale. An occurrence has no id of its own - it
        // is derived from a series and a start - so anything cancelled or moved
        // could not be found again to remove individually.
        LocalNotificationCenter.Current.CancelAll();

        var scheduled = 0;

        foreach (var occurrence in occurrences.OrderBy(o => o.StartsUtc).Take(MaxPending))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var notifyAt = occurrence.StartsUtc.AddMinutes(-occurrence.ReminderMinutesBefore);

            // A reminder whose lead time has already passed would fire instantly
            // and look like a bug. The event itself is still ahead, so this is
            // ordinary for anything starting within the next few minutes.
            if (notifyAt <= DateTimeOffset.UtcNow)
            {
                continue;
            }

            var request = new NotificationRequest
            {
                NotificationId = NotificationIdFor(occurrence),
                Title = occurrence.Title,
                Description = DescribeWhen(occurrence),
                Schedule = new NotificationRequestSchedule
                {
                    // Local time, because that is what the scheduler expects and
                    // what the device's own clock is set to.
                    NotifyTime = notifyAt.LocalDateTime,
                    Android = new AndroidScheduleOptions
                    {
                        // Must be set explicitly. The plugin's Default mode is
                        // setExactAndAllowWhileIdle, which needs the restricted
                        // SCHEDULE_EXACT_ALARM permission on Android 12+ - a
                        // permission this app deliberately does not request, so
                        // leaving the default would break delivery on any modern
                        // phone. InexactAllowWhileIdle still fires during Doze,
                        // and a calendar nudge a minute adrift is fine.
                        ScheduleMode = AndroidScheduleMode.InexactAllowWhileIdle,
                    },
                },
                Android = new AndroidOptions
                {
                    ChannelId = ReminderChannelId,
                    LaunchAppWhenTapped = true,
                },
            };

            await LocalNotificationCenter.Current.Show(request);
            scheduled++;
        }

        ReminderLog.RemindersScheduled(logger, scheduled, occurrences.Count);

        return scheduled;
    }

    /// <summary>The channel reminders are delivered on. Created at startup.</summary>
    public const string ReminderChannelId = "ada_reminders";

    /// <summary>
    /// A stable id per occurrence, so a re-sync that produces the same reminder
    /// replaces it rather than stacking a duplicate. Series id and start
    /// together are what identify an occurrence; the modulo keeps it inside the
    /// positive int range the platform requires.
    /// </summary>
    private static int NotificationIdFor(EventOccurrenceDto occurrence) =>
        Math.Abs(HashCode.Combine(occurrence.EventId, occurrence.StartsUtc)) % int.MaxValue;

    private static string DescribeWhen(EventOccurrenceDto occurrence)
    {
        var local = occurrence.StartsUtc.ToLocalTime();

        var when = occurrence.IsAllDay
            ? local.ToString("dddd d MMMM", null)
            : local.ToString("HH:mm", null);

        return string.IsNullOrWhiteSpace(occurrence.Location)
            ? when
            : $"{when} — {occurrence.Location}";
    }
}

/// <summary>Source-generated logging, per the CA1848 policy.</summary>
internal static partial class ReminderLog
{
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Information,
        Message = "Scheduled {Scheduled} of {Candidates} upcoming reminders.")]
    public static partial void RemindersScheduled(ILogger logger, int scheduled, int candidates);
}
