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
    public static bool IsTracking => Microsoft.Maui.Storage.Preferences.Default.Get(TrackingPreferenceKey, false);
    private const int NotificationId = 4107;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMinutes(1);
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

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consent = await api.GetLocationConsentAsync(cancellationToken);
                    if (!consent.SharingEnabled)
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
                catch (Exception)
                {
                    // Keep the foreground service alive and retry on the next interval.
                    UpdateNotification("Location update unavailable; retrying…");
                }

                await Task.Delay(UpdateInterval, cancellationToken);
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

    private void StopTracking()
    {
        Microsoft.Maui.Storage.Preferences.Default.Set(TrackingPreferenceKey, false);
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
