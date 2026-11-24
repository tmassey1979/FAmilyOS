using System.Net;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Content.PM;
using Microsoft.Maui;
using Microsoft.Maui.Devices.Sensors;

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
    private static readonly TimeSpan MovingUpdateInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StationaryUpdateInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LowBatteryMovingInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LowBatteryStationaryInterval = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan CriticalBatteryMovingInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CriticalBatteryStationaryInterval = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan StationaryDetectionWindow = TimeSpan.FromSeconds(30);
    private const double MotionAccelerationThreshold = 1.0;
    private static readonly TimeSpan MaximumRetryInterval = TimeSpan.FromMinutes(5);
    private readonly object _motionLock = new();
    private readonly SemaphoreSlim _motionSignal = new(0, 1);
    private DateTime _lastMotionAtUtc = DateTime.UtcNow;
    private bool _motionSensorStarted;
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

            StartMotionMonitoring();
            var consecutiveFailures = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var cycleStarted = System.Diagnostics.Stopwatch.GetTimestamp();
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
                        var mode = GetTrackingMode();
                        UpdateNotification($"Last update: {location.Timestamp.ToLocalTime():t} · {mode.Description}");
                    }
                    else
                    {
                        UpdateNotification("Waiting for an accurate location fix…");
                    }
                }
                catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (FamilyOS.Maui.Services.FamilyApiException ex)
                    when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    // Authentication or household authorization cannot recover by
                    // retrying location uploads. Stop collection and require the user
                    // to reopen the app and resolve their session/access.
                    UpdateNotification("FamilyOS access expired or was revoked; tracking stopped.");
                    break;
                }
                catch (UnauthorizedAccessException)
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

                // Adapt GPS/network activity to battery and motion. Accelerometer monitoring
                // remains active while GPS sleeps so movement can wake the tracking loop.
                var mode = GetTrackingMode();
                var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(cycleStarted);
                var interval = elapsed >= mode.Interval ? TimeSpan.Zero : mode.Interval - elapsed;
                if (retryDelay > interval)
                    interval = retryDelay;

                // Refresh the heartbeat during long stationary/low-battery waits so the
                // UI does not mistake intentional idle time for a dead service.
                Microsoft.Maui.Storage.Preferences.Default.Set(
                    TrackingHeartbeatKey, DateTime.UtcNow.ToString("O"));
                await WaitForNextUpdateAsync(interval, cancellationToken);
            }
        }
        catch (System.OperationCanceledException)
        {
        }
        finally
        {
            StopMotionMonitoring();
            StopSelf();
        }
    }

    private void StartMotionMonitoring()
    {
        try
        {
            var accelerometer = Accelerometer.Default;
            if (!accelerometer.IsSupported)
                return;

            lock (_motionLock)
                _lastMotionAtUtc = DateTime.UtcNow; // Start in fast mode until stationary is established.

            accelerometer.ReadingChanged += OnAccelerometerReadingChanged;
            accelerometer.Start(SensorSpeed.UI);
            _motionSensorStarted = true;
        }
        catch (FeatureNotSupportedException)
        {
            _motionSensorStarted = false;
        }
        catch (InvalidOperationException)
        {
            _motionSensorStarted = false;
        }
    }

    private void OnAccelerometerReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        var acceleration = e.Reading.Acceleration;
        var magnitude = Math.Sqrt(
            acceleration.X * acceleration.X +
            acceleration.Y * acceleration.Y +
            acceleration.Z * acceleration.Z);
        var deviation = Math.Abs(magnitude - 9.80665);
        if (deviation < MotionAccelerationThreshold)
            return;

        var wakeTracking = false;
        lock (_motionLock)
        {
            var wasStationary = DateTime.UtcNow - _lastMotionAtUtc >= StationaryDetectionWindow;
            _lastMotionAtUtc = DateTime.UtcNow;
            wakeTracking = wasStationary;
        }

        if (wakeTracking)
        {
            try { _motionSignal.Release(); }
            catch (SemaphoreFullException) { }
        }
    }

    private void StopMotionMonitoring()
    {
        if (!_motionSensorStarted)
            return;

        try
        {
            Accelerometer.Default.ReadingChanged -= OnAccelerometerReadingChanged;
            if (Accelerometer.Default.IsMonitoring)
                Accelerometer.Default.Stop();
        }
        catch (Exception)
        {
            // Sensor cleanup must not prevent the foreground service from stopping.
        }
        finally
        {
            _motionSensorStarted = false;
        }
    }

    private (TimeSpan Interval, string Description) GetTrackingMode()
    {
        var battery = Battery.Default;
        var batteryLevel = battery.ChargeLevel;
        var charging = battery.State is BatteryState.Charging or BatteryState.Full;
        bool moving;
        lock (_motionLock)
            moving = !_motionSensorStarted || DateTime.UtcNow - _lastMotionAtUtc < StationaryDetectionWindow;

        // Charging devices can use the normal cadence. Otherwise progressively
        // reduce GPS and upload frequency as the battery becomes depleted.
        if (charging)
            return moving
                ? (MovingUpdateInterval, "moving · 5s updates")
                : (StationaryUpdateInterval, "stationary · 60s updates");

        if (batteryLevel is >= 0 and <= 0.08)
            return moving
                ? (CriticalBatteryMovingInterval, "low battery · 30s updates")
                : (CriticalBatteryStationaryInterval, "low battery, idle · 3m updates");

        if (batteryLevel is >= 0 and <= 0.15)
            return moving
                ? (LowBatteryMovingInterval, "low battery · 15s updates")
                : (LowBatteryStationaryInterval, "low battery, idle · 90s updates");

        return moving
            ? (MovingUpdateInterval, "moving · 5s updates")
            : (StationaryUpdateInterval, "stationary · 60s updates");
    }

    private async Task WaitForNextUpdateAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        if (interval <= TimeSpan.Zero)
            return;

        if (!_motionSensorStarted)
        {
            await Task.Delay(interval, cancellationToken);
            return;
        }

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(interval, waitCancellation.Token);
        var motionTask = _motionSignal.WaitAsync(waitCancellation.Token);
        await Task.WhenAny(delayTask, motionTask);
        waitCancellation.Cancel();
        try { await Task.WhenAll(delayTask, motionTask); }
        catch (OperationCanceledException) { }
        cancellationToken.ThrowIfCancellationRequested();
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
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMyLocation)
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
