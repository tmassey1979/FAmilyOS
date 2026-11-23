using Android.App;
using Android.Content;
using Android.OS;
using Android.Content.PM;
using Microsoft.Maui;

namespace FamilyOS.Maui.Platforms.Android;

/// <summary>
/// Explicitly started Android location foreground service. It never starts
/// itself, is not sticky, and stops when the user taps Stop or sharing is revoked.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class LocationTrackingForegroundService : Service
{
    public const string StopAction = "com.familyos.app.location.STOP";
    private const string ChannelId = "familyos_location_tracking";
    private const string TrackingPreferenceKey = "familyos.location.continuous-tracking";
    private const string TrackingHeartbeatKey = "familyos.location.tracking-heartbeat";
    public static bool IsTracking
    {
        get
        {
            var preferences = Microsoft.Maui.Storage.Preferences.Default;
            if (!preferences.Get(TrackingPreferenceKey, false)) return false;
            var raw = preferences.Get(TrackingHeartbeatKey, string.Empty);
            if (!DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var heartbeat)
                || DateTime.UtcNow - heartbeat.ToUniversalTime() > TimeSpan.FromMinutes(6))
            {
                preferences.Set(TrackingPreferenceKey, false);
                return false;
            }
            return true;
        }
    }
    private const int NotificationId = 4107;
    private static readonly TimeSpan NormalUpdateInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PowerSaveUpdateInterval = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan LowBatteryUpdateInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaximumRetryInterval = TimeSpan.FromMinutes(5);
    private CancellationTokenSource? _cancellation;
    private Task? _trackingTask;

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == StopAction)
        {
            StopTracking();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (_trackingTask is not null)
            return StartCommandResult.NotSticky;

        var notification = BuildNotification("Starting location updates…");
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            StartForeground(NotificationId, notification, ForegroundService.TypeLocation);
        else
            StartForeground(NotificationId, notification);

        Microsoft.Maui.Storage.Preferences.Default.Set(TrackingPreferenceKey, true);
        Microsoft.Maui.Storage.Preferences.Default.Set(TrackingHeartbeatKey, DateTime.UtcNow.ToString("O"));
        _cancellation = new CancellationTokenSource();
        _trackingTask = TrackAsync(_cancellation.Token);
        return StartCommandResult.NotSticky;
    }

    private async Task TrackAsync(CancellationToken cancellationToken)
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            var deviceLocation = services?.GetService<FamilyOS.Maui.Services.DeviceLocationService>();
            var api = services?.GetService<FamilyOS.Maui.Services.FamilyApiClient>();
            if (deviceLocation is null || api is null || !deviceLocation.IsEnabled)
            {
                StopSelf();
                return;
            }

            var consecutiveFailures = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var retryDelay = TimeSpan.Zero;
                try
                {
                    Microsoft.Maui.Storage.Preferences.Default.Set(TrackingHeartbeatKey, DateTime.UtcNow.ToString("O"));
                    var consent = await api.GetLocationConsentAsync(cancellationToken);
                    if (!consent.SharingEnabled)
                        break;

                    // Tracking requires a continuing purpose: at least one authorized viewer
                    // or the owner's separate personal-history opt-in. A revoked final grant
                    // must stop collection instead of retrying uploads indefinitely.
                    var viewers = await api.GetLocationViewersAsync(cancellationToken);
                    var history = await api.GetLocationHistoryConsentAsync(cancellationToken);
                    if (!viewers.Any(x => x.CanView) && !history.HistoryEnabled)
                        break;

                    var location = await deviceLocation.GetCurrentLocationAsync(cancellationToken);
                    if (location?.Accuracy is double accuracy)
                    {
                        await api.ShareLocationAsync(
                            location.Latitude,
                            location.Longitude,
                            accuracy,
                            location.Timestamp.UtcDateTime,
                            cancellationToken);
                        var batteryConsent = await api.GetBatteryConsentAsync(cancellationToken);
                        if (batteryConsent.BatterySharingEnabled && Battery.Default.ChargeLevel >= 0)
                        {
                            var battery = Battery.Default;
                            var percent = Math.Clamp((int)Math.Round(battery.ChargeLevel * 100), 0, 100);
                            var charging = battery.State is BatteryState.Charging or BatteryState.Full;
                            await api.ShareBatteryStatusAsync(percent, charging, cancellationToken);
                        }
                        consecutiveFailures = 0;
                        UpdateNotification($"Last update: {location.Timestamp.ToLocalTime():t}");
                    }
                    else
                    {
                        UpdateNotification("Waiting for an accurate location fix…");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    UpdateNotification("Location permission was revoked; tracking stopped.");
                    break;
                }
                catch (PermissionException)
                {
                    UpdateNotification("Location permission was revoked; tracking stopped.");
                    break;
                }
                catch (Exception)
                {
                    // Keep the foreground service alive and retry on the next interval.
                    consecutiveFailures = Math.Min(consecutiveFailures + 1, 8);
                    var retrySeconds = Math.Min(MaximumRetryInterval.TotalSeconds,
                        30 * Math.Pow(2, consecutiveFailures - 1));
                    retryDelay = TimeSpan.FromSeconds(retrySeconds);
                    UpdateNotification($"Connection unavailable; retrying in {(int)retryDelay.TotalSeconds}s…");
                }

                // Respect Android power-save mode and low battery by reducing GPS/network
                // activity instead of sampling at the normal one-minute interval.
                var interval = GetPowerAwareInterval();
                if (retryDelay > interval)
                    interval = retryDelay;

                // Refresh the heartbeat before intentional idle time so stale-heartbeat
                // detection still distinguishes a sleeping service from a killed service.
                Microsoft.Maui.Storage.Preferences.Default.Set(
                    TrackingHeartbeatKey, DateTime.UtcNow.ToString("O"));
                await Task.Delay(interval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            StopSelf();
        }
    }

    private TimeSpan GetPowerAwareInterval()
    {
        var battery = Battery.Default;
        if (battery.ChargeLevel is >= 0 and <= 0.15)
            return LowBatteryUpdateInterval;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            var power = (PowerManager?)GetSystemService(PowerService);
            if (power?.IsPowerSaveMode == true)
                return PowerSaveUpdateInterval;
        }

        return NormalUpdateInterval;
    }

    private void StopTracking()
    {
        Microsoft.Maui.Storage.Preferences.Default.Set(TrackingPreferenceKey, false);
        Microsoft.Maui.Storage.Preferences.Default.Remove(TrackingHeartbeatKey);
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _trackingTask = null;
        StopForeground(StopForegroundFlags.Remove);
    }

    public override void OnDestroy()
    {
        StopTracking();
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId, "FamilyOS location sharing", NotificationImportance.Low)
        {
            Description = "Visible notification while household location sharing is active."
        });
    }

    private Notification BuildNotification(string status)
    {
        var stopIntent = new Intent(this, typeof(LocationTrackingForegroundService));
        stopIntent.SetAction(StopAction);
        var stopPendingIntent = PendingIntent.GetService(
            this, 4108, stopIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);

        return builder
            .SetContentTitle("FamilyOS location sharing is active")
            .SetContentText(status)
            .SetSmallIcon(Android.Resource.Drawable.IcMenuMyLocation)
            .SetOngoing(true)
            .SetCategory(Notification.CategoryService)
            .AddAction(new Notification.Action.Builder(null, "Stop", stopPendingIntent).Build())
            .Build();
    }

    private void UpdateNotification(string status)
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.Notify(NotificationId, BuildNotification(status));
    }
}

/// <summary>Starts tracking only from an explicit user action in the visible app.</summary>
public static class LocationTrackingController
{
    public static void Start()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(LocationTrackingForegroundService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    public static void Stop()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(LocationTrackingForegroundService));
        intent.SetAction(LocationTrackingForegroundService.StopAction);
        context.StartService(intent);
    }
}
