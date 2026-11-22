using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// First location milestone: explicit device consent and a user-initiated
/// foreground fix. Household sharing is intentionally not implied or enabled.
/// </summary>
public sealed class LocationPage : ContentPage
{
    private readonly DeviceLocationService _location;
    private readonly FamilyApiClient _api;
    private readonly Label _sharingStatus;
    private readonly Label _familyStatus;
    private bool _sharingEnabled;
    private readonly Label _consentStatus;
    private readonly Label _fixStatus;
    private readonly Button _enable;
    private readonly Button _update;
    private readonly Button _disable;
    private readonly Button _enableSharing;
    private readonly Button _shareNow;
    private readonly Button _stopSharing;
    private readonly Button _refreshFamily;
    private Location? _lastLocation;
    private bool _busy;

    public LocationPage(DeviceLocationService location, FamilyApiClient api)
    {
        _location = location;
        _api = api;
        Title = "Location";

        var heading = new Label
        {
            Text = "Your location",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");

        var explanation = new Label
        {
            Text = "Location access is optional. FamilyOS only checks your location when you ask. Household sharing is a separate opt-in, and coordinates are sent only when you explicitly tap Share this location. There is no background tracking.",
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };
        explanation.SetDynamicResource(VisualElement.StyleProperty, "TextBody");

        _consentStatus = new Label { FontSize = 13 };
        _consentStatus.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _fixStatus = new Label
        {
            Text = "No location has been requested.",
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _fixStatus.SetDynamicResource(VisualElement.StyleProperty, "TextBody");

        _enable = new Button { Text = "Enable location on this device" };
        _enable.Clicked += async (_, _) => await EnableAsync();
        _update = new Button { Text = "Get my current location", IsEnabled = false };
        _update.Clicked += async (_, _) => await UpdateAsync();
        _disable = new Button { Text = "Turn off device location access", IsEnabled = false };
        _disable.Clicked += async (_, _) => await DisableAsync();

        _sharingStatus = new Label { FontSize = 13 };
        _sharingStatus.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _enableSharing = new Button { Text = "Enable household sharing", IsEnabled = false };
        _enableSharing.Clicked += async (_, _) => await SetSharingAsync(true);
        _shareNow = new Button { Text = "Share this location with my household", IsEnabled = false };
        _shareNow.Clicked += async (_, _) => await ShareCurrentLocationAsync();
        _stopSharing = new Button { Text = "Stop household sharing", IsEnabled = false };
        _stopSharing.Clicked += async (_, _) => await SetSharingAsync(false);
        _familyStatus = new Label { Text = "Household locations have not been loaded.", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
        _familyStatus.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _refreshFamily = new Button { Text = "Refresh household locations", IsEnabled = true };
        _refreshFamily.Clicked += async (_, _) => await RefreshFamilyLocationsAsync();

        var privacy = new Label
        {
            Text = "Privacy: device permission and household sharing are separate choices. A shared location is visible to active members of your household and is retained as the latest fix until you stop sharing. Stopping sharing immediately removes the saved coordinates.",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        };
        privacy.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var card = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children = { heading, explanation, _consentStatus, _enable, _update, _disable, _sharingStatus, _enableSharing, _shareNow, _stopSharing, _familyStatus, _refreshFamily, _fixStatus, privacy }
            }
        };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 16),
                Spacing = 14,
                Children = { card }
            }
        };

        RefreshState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RefreshState();
        await LoadSharingStateAsync();
    }

    private void RefreshState()
    {
        var enabled = _location.IsEnabled;
        _consentStatus.Text = enabled
            ? "Device location access is enabled by your choice."
            : "Device location access is off.";
        _enable.IsVisible = !enabled;
        _update.IsEnabled = enabled && !_busy;
        _disable.IsEnabled = enabled && !_busy;
        _enableSharing.IsEnabled = !_busy && enabled && !_sharingEnabled;
        _shareNow.IsEnabled = !_busy && enabled && _sharingEnabled && _lastLocation is not null;
        _stopSharing.IsEnabled = !_busy && _sharingEnabled;
    }

    private async Task LoadSharingStateAsync()
    {
        try
        {
            var consent = await _api.GetLocationConsentAsync();
            _sharingEnabled = consent.SharingEnabled;
            _sharingStatus.Text = _sharingEnabled
                ? "Household sharing is enabled. Only locations you explicitly submit are shared."
                : "Household sharing is off. Your coordinates are not sent to the server.";
            await RefreshFamilyLocationsAsync();
        }
        catch (Exception ex)
        {
            _sharingStatus.Text = $"Could not load household sharing settings: {ex.Message}";
        }
        RefreshState();
    }

    private async Task SetSharingAsync(bool enabled)
    {
        if (enabled && !await DisplayAlertAsync("Enable household location sharing?",
            "Active members of your household will be able to see the latest location you explicitly share. This does not enable background tracking.",
            "Enable sharing", "Cancel")) return;
        try
        {
            var result = await _api.SetLocationSharingAsync(enabled);
            _sharingEnabled = result.SharingEnabled;
            if (!enabled) _lastLocation = null;
            _sharingStatus.Text = enabled
                ? "Household sharing is on. Your location is not sent until you explicitly share a reading."
                : "Household sharing is off. Your saved coordinates have been removed.";
            RefreshState();
            await RefreshFamilyLocationsAsync();
        }
        catch (Exception ex)
        {
            _sharingStatus.Text = $"Could not update household sharing: {ex.Message}";
        }
    }

    private async Task ShareCurrentLocationAsync()
    {
        if (!_sharingEnabled || _lastLocation is null) return;
        if (!_lastLocation.Accuracy.HasValue)
        {
            _fixStatus.Text = "Android did not provide an accuracy estimate. Get another fix before sharing.";
            return;
        }
        try
        {
            await _api.ShareLocationAsync(
                _lastLocation.Latitude,
                _lastLocation.Longitude,
                _lastLocation.Accuracy.Value,
                _lastLocation.Timestamp.UtcDateTime);
            _fixStatus.Text = "Your current location was shared with active household members.";
            await RefreshFamilyLocationsAsync();
        }
        catch (Exception ex)
        {
            _fixStatus.Text = $"Could not share this location: {ex.Message}";
        }
    }

    private async Task RefreshFamilyLocationsAsync()
    {
        try
        {
            var locations = await _api.GetFamilyLocationsAsync();
            _familyStatus.Text = locations.Count == 0
                ? "No household members have shared a recent location."
                : string.Join("\n", locations.Select(x =>
                    $"{x.DisplayName}: {x.Latitude:F5}, {x.Longitude:F5} · {x.UpdatedAtUtc?.ToLocalTime():g}"));
        }
        catch (Exception ex)
        {
            _familyStatus.Text = $"Could not load household locations: {ex.Message}";
        }
    }

    private async Task EnableAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var enabled = await _location.EnableAsync();
            if (!enabled)
            {
                _fixStatus.Text = "Android did not grant location permission. You can enable it later in Android app settings.";
                return;
            }
            _fixStatus.Text = "Location is enabled on this device. Nothing is being shared. Choose Get my current location when you want a one-time reading.";
        }
        catch (Exception ex)
        {
            _fixStatus.Text = $"Could not enable location: {ex.Message}";
        }
        finally { SetBusy(false); }
    }

    private async Task UpdateAsync()
    {
        if (_busy || !_location.IsEnabled) return;
        SetBusy(true);
        _fixStatus.Text = "Getting a location fix…";
        try
        {
            _lastLocation = await _location.GetCurrentLocationAsync();
            if (_lastLocation is null)
            {
                _fixStatus.Text = "Android could not determine your location. Check that device location services are on and try again.";
                return;
            }

            var accuracy = _lastLocation.Accuracy.HasValue
                ? $"±{_lastLocation.Accuracy.Value:0} m"
                : "accuracy unavailable";
            _fixStatus.Text =
                $"Latitude: {_lastLocation.Latitude:F6}\n" +
                $"Longitude: {_lastLocation.Longitude:F6}\n" +
                $"Accuracy: {accuracy}\n" +
                $"Recorded: {_lastLocation.Timestamp.ToLocalTime():g}";
        }
        catch (FeatureNotEnabledException)
        {
            _fixStatus.Text = "Device location services are turned off. Turn them on in Android settings and try again.";
        }
        catch (PermissionException)
        {
            _fixStatus.Text = "Location permission is no longer available. Turn it on in Android app settings.";
        }
        catch (Exception ex)
        {
            _fixStatus.Text = $"Could not get a location fix: {ex.Message}";
        }
        finally { SetBusy(false); }
        RefreshState();
    }

    private async Task DisableAsync()
    {
        if (_busy) return;
        if (!await DisplayAlertAsync("Turn off location access?",
            "FamilyOS will stop requesting your device location. Android permission can also be revoked in app settings.",
            "Turn off", "Cancel")) return;

        _location.Disable();
        _lastLocation = null;
        if (_sharingEnabled)
        {
            try
            {
                await _api.SetLocationSharingAsync(false);
                _sharingEnabled = false;
                _sharingStatus.Text = "Household sharing is off and saved coordinates have been removed.";
            }
            catch (Exception ex)
            {
                _sharingStatus.Text = $"Device location is off, but server sharing could not be revoked. Reconnect and tap Stop household sharing. {ex.Message}";
            }
        }
        _fixStatus.Text = "Device location access is off. No new location readings can be requested from this screen.";
        RefreshState();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _enable.IsEnabled = !busy;
        RefreshState();
    }
}
