namespace FamilyOS.Maui.Services;

/// <summary>
/// Foreground-only device location access. Coordinates remain in memory and are
/// never uploaded or persisted by this service.
/// </summary>
public sealed class DeviceLocationService
{
    private const string ConsentKey = "familyos.location.device-consent";

    public bool IsEnabled => Preferences.Default.Get(ConsentKey, false);

    public async Task<bool> EnableAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
            return false;

        Preferences.Default.Set(ConsentKey, true);
        return true;
    }

    public void Disable()
    {
        Preferences.Default.Set(ConsentKey, false);
    }

    public async Task<Location?> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("Enable location access on this device first.");

        var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
            throw new UnauthorizedAccessException("Location permission is not currently granted.");

        var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(4));
        return await Geolocation.Default.GetLocationAsync(request, cancellationToken);
    }
}
