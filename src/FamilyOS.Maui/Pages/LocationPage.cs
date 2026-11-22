using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Manages separate device permission and household-sharing consent. Each
/// shared location requires a user-initiated foreground fix and explicit upload.
/// </summary>
public sealed class LocationPage : ContentPage
{
    private readonly DeviceLocationService _location;
    private readonly FamilyApiClient _api;
    private readonly Label _sharingStatus;
    private readonly Label _familyStatus;
    private readonly VerticalStackLayout _viewerControls;
    private readonly Label _viewerStatus;
    private bool _sharingEnabled;
    private bool _hasViewerGrant;
    private bool _historyEnabled;
    private readonly Label _trackingStatus;
    private readonly Button _startTracking;
    private readonly Button _stopTracking;
    private readonly Switch _historySwitch;
    private readonly Label _historyStatus;
    private readonly Button _clearHistory;
    private bool _loadingHistorySwitch;
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
            Text = "Location access is optional. FamilyOS only checks your location when you ask. Household sharing is a separate opt-in, and coordinates are sent only when you explicitly tap Share this location. Continuous tracking is available only after you explicitly start it; Android keeps a visible foreground-service notification while it runs.",
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
        _trackingStatus = new Label { Text = "Continuous tracking is off.", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
        _trackingStatus.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _startTracking = new Button { Text = "Start continuous location sharing", IsEnabled = false };
        _startTracking.Clicked += async (_, _) => await StartTrackingAsync();
        _stopTracking = new Button { Text = "Stop continuous location sharing", IsEnabled = false };
        _stopTracking.Clicked += (_, _) => StopTracking();
        _historySwitch = new Switch { IsToggled = false, HorizontalOptions = LayoutOptions.Start };
        _historySwitch.Toggled += async (_, e) =>
        {
            if (!_loadingHistorySwitch) await SetHistoryEnabledAsync(e.Value);
        };
        _historyStatus = new Label { Text = "Location history is off. When enabled, uploaded points are retained for up to 30 days.", FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
        _historyStatus.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _clearHistory = new Button { Text = "Delete my saved location history", FontSize = 12 };
        _clearHistory.Clicked += async (_, _) => await ClearHistoryAsync();
        _shareNow.Clicked += async (_, _) => await ShareCurrentLocationAsync();
        _stopSharing = new Button { Text = "Stop household sharing", IsEnabled = false };
        _stopSharing.Clicked += async (_, _) => await SetSharingAsync(false);
        _viewerStatus = new Label { Text = "Choose exactly who can view your shared location.", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
        _viewerStatus.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _viewerControls = new VerticalStackLayout { Spacing = 8 };
        _familyStatus = new Label { Text = "Household locations have not been loaded.", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
        _familyStatus.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _refreshFamily = new Button { Text = "Refresh household locations", IsEnabled = true };
        _refreshFamily.Clicked += async (_, _) => await RefreshFamilyLocationsAsync();

        var privacy = new Label
        {
            Text = "Privacy: device permission and household sharing are separate choices. You choose each household member who can view your location. New members get no access automatically. Stopping sharing immediately removes saved coordinates and all viewer grants.",
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
                Children = { heading, explanation, _consentStatus, _enable, _update, _disable, _sharingStatus, _enableSharing, _viewerStatus, _viewerControls, _shareNow, _trackingStatus, _startTracking, _stopTracking, new Label { Text = "Location history (separate opt-in)", FontSize = 15, FontAttributes = FontAttributes.Bold }, _historySwitch, _historyStatus, _clearHistory, _stopSharing, _familyStatus, _refreshFamily, _fixStatus, privacy }
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
        _shareNow.IsEnabled = !_busy && enabled && _sharingEnabled && _hasViewerGrant && _lastLocation is not null;
        _stopSharing.IsEnabled = !_busy && _sharingEnabled;
        _startTracking.IsEnabled = !_busy && enabled && _sharingEnabled && (_hasViewerGrant || _historyEnabled);
#if ANDROID
        var tracking = FamilyOS.Maui.Platforms.Android.LocationTrackingForegroundService.IsTracking;
        _trackingStatus.Text = tracking
            ? "Continuous tracking is active. Android's persistent notification lets you stop it at any time."
            : "Continuous tracking is off.";
        _stopTracking.IsEnabled = !_busy && tracking;
#else
        _trackingStatus.Text = "Continuous tracking is currently supported on Android only.";
        _stopTracking.IsEnabled = false;
#endif
    }

    private async Task LoadSharingStateAsync()
    {
        try
        {
            var consent = await _api.GetLocationConsentAsync();
            _sharingEnabled = consent.SharingEnabled;
            _sharingStatus.Text = _sharingEnabled
                ? "Household sharing is enabled. Locations are shared while continuous tracking is active or when you manually submit a reading."
                : "Household sharing is off. Your coordinates are not sent to the server.";
            await LoadViewerGrantsAsync();
            await LoadHistoryConsentAsync();
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
            "Only household members you explicitly allow in the access list below can see the latest location you share. This does not enable background tracking.",
            "Enable sharing", "Cancel")) return;
        try
        {
            if (!enabled) StopTracking();
            var result = await _api.SetLocationSharingAsync(enabled);
            _sharingEnabled = result.SharingEnabled;
            if (!enabled) _lastLocation = null;
            _sharingStatus.Text = enabled
                ? "Household sharing is on. Only people you explicitly allow can see a reading, and nothing is sent until you start tracking or manually share a reading."
                : "Household sharing is off. Your saved coordinates have been removed.";
            RefreshState();
            await LoadViewerGrantsAsync();
            await LoadHistoryConsentAsync();
            await RefreshFamilyLocationsAsync();
        }
        catch (Exception ex)
        {
            _sharingStatus.Text = $"Could not update household sharing: {ex.Message}";
        }
    }

    private async Task LoadHistoryConsentAsync()
    {
        _loadingHistorySwitch = true;
        try
        {
            var result = await _api.GetLocationHistoryConsentAsync();
            _historyEnabled = result.HistoryEnabled;
            _historySwitch.IsToggled = result.HistoryEnabled;
            _historyStatus.Text = result.HistoryEnabled
                ? "Location history is on. Uploaded points are visible only to people you allowed and are automatically deleted after 30 days."
                : "Location history is off. No route history is being stored.";
        }
        catch (Exception ex)
        {
            _historyEnabled = false;
            _historyStatus.Text = $"Could not load history settings: {ex.Message}";
        }
        finally
        {
            _loadingHistorySwitch = false;
            _historySwitch.IsEnabled = _sharingEnabled && !_busy;
            _clearHistory.IsEnabled = _sharingEnabled && !_busy;
        }
    }

    private async Task SetHistoryEnabledAsync(bool enabled)
    {
        if (enabled && !_sharingEnabled)
        {
            _loadingHistorySwitch = true;
            _historySwitch.IsToggled = false;
            _loadingHistorySwitch = false;
            _historyStatus.Text = "Enable household sharing before turning on location history.";
            return;
        }
        var accepted = await DisplayAlertAsync(
            enabled ? "Enable location history?" : "Turn off and delete location history?",
            enabled
                ? "FamilyOS will save future uploaded location points for up to 30 days. Only household members you explicitly allow can view them. You can delete them at any time."
                : "This immediately deletes your stored location history and stops recording future points.",
            enabled ? "Enable history" : "Turn off and delete", "Cancel");
        if (!accepted)
        {
            _loadingHistorySwitch = true;
            _historySwitch.IsToggled = !enabled;
            _loadingHistorySwitch = false;
            return;
        }
        try
        {
            var result = await _api.SetLocationHistoryConsentAsync(enabled);
            _loadingHistorySwitch = true;
            _historyEnabled = result.HistoryEnabled;
            _historySwitch.IsToggled = result.HistoryEnabled;
            _loadingHistorySwitch = false;
            _historyStatus.Text = result.HistoryEnabled
                ? "Location history is on. Uploaded points are retained for up to 30 days."
                : "Location history is off and stored points have been deleted.";
            RefreshState();
        }
        catch (Exception ex)
        {
            _loadingHistorySwitch = true;
            _historySwitch.IsToggled = !enabled;
            _historyEnabled = !enabled;
            _loadingHistorySwitch = false;
            _historyStatus.Text = $"Could not update history settings: {ex.Message}";
            RefreshState();
        }
    }

    private async Task ClearHistoryAsync()
    {
        if (!await DisplayAlertAsync("Delete location history?",
            "Permanently delete all location history currently stored for your account? This does not turn off future history recording.",
            "Delete history", "Cancel")) return;
        try
        {
            await _api.ClearLocationHistoryAsync();
            _historyStatus.Text = _historySwitch.IsToggled
                ? "Saved history deleted. New points will be recorded while history remains enabled."
                : "Saved location history deleted.";
        }
        catch (Exception ex)
        {
            _historyStatus.Text = $"Could not delete location history: {ex.Message}";
        }
    }

    private async Task LoadViewerGrantsAsync()
    {
        _viewerControls.Children.Clear();
        if (!_sharingEnabled)
        {
            _hasViewerGrant = false;
            _viewerStatus.Text = "Enable household sharing before granting anyone access to your location.";
            return;
        }
        try
        {
            var viewers = await _api.GetLocationViewersAsync();
            _viewerControls.Children.Clear();
            _hasViewerGrant = viewers.Any(x => x.CanView);
            if (viewers.Count == 0)
            {
                _viewerStatus.Text = "There are no other active household members to grant access to.";
                RefreshState();
                return;
            }

            _viewerStatus.Text = "Location access is private until you explicitly allow a person below.";
            RefreshState();
            foreach (var viewer in viewers)
            {
                var row = new HorizontalStackLayout
                {
                    Spacing = 10,
                    VerticalOptions = LayoutOptions.Center
                };
                var name = new Label
                {
                    Text = viewer.DisplayName,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.FillAndExpand
                };
                name.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
                var action = new Button
                {
                    Text = viewer.CanView ? "Revoke access" : "Allow access",
                    FontSize = 12,
                    Padding = new Thickness(10, 6),
                    CommandParameter = viewer.MemberId
                };
                action.Clicked += async (_, _) => await ChangeViewerGrantAsync(
                    viewer.MemberId, !viewer.CanView, viewer.DisplayName);
                row.Children.Add(name);
                row.Children.Add(action);
                _viewerControls.Children.Add(row);
            }
        }
        catch (Exception ex)
        {
            _hasViewerGrant = false;
            _viewerStatus.Text = $"Could not load location access list: {ex.Message}";
            RefreshState();
        }
    }

    private async Task ChangeViewerGrantAsync(Guid viewerMemberId, bool enabled, string displayName)
    {
        if (enabled && !await DisplayAlertAsync("Allow location access?",
            $"{displayName} will be able to see the latest location you share. This does not enable tracking or share your current coordinates by itself.",
            "Allow", "Cancel")) return;
        if (!enabled && !await DisplayAlertAsync("Revoke location access?",
            $"{displayName} will no longer be able to see your shared location.",
            "Revoke", "Cancel")) return;

        try
        {
            await _api.SetLocationViewerAsync(viewerMemberId, enabled);
            await LoadViewerGrantsAsync();
            await LoadHistoryConsentAsync();
            await RefreshFamilyLocationsAsync();
        }
        catch (Exception ex)
        {
            _viewerStatus.Text = $"Could not update access for {displayName}: {ex.Message}";
        }
    }

    private async Task ShareCurrentLocationAsync()
    {
        if (!_sharingEnabled || !_hasViewerGrant || _lastLocation is null) return;
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
            _fixStatus.Text = "Your current location was shared with the household members you explicitly allowed.";
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

        StopTracking();
        _location.Disable();
        _lastLocation = null;
        if (_sharingEnabled)
        {
            try
            {
                await _api.SetLocationSharingAsync(false);
                _sharingEnabled = false;
                _sharingStatus.Text = "Household sharing is off and saved coordinates have been removed.";
                await LoadViewerGrantsAsync();
            }
            catch (Exception ex)
            {
                _sharingStatus.Text = $"Device location is off, but server sharing could not be revoked. Reconnect and tap Stop household sharing. {ex.Message}";
            }
        }
        _fixStatus.Text = "Device location access is off. No new location readings can be requested from this screen.";
        RefreshState();
    }

    private async Task StartTrackingAsync()
    {
        if (!_location.IsEnabled || !_sharingEnabled || (!_hasViewerGrant && !_historyEnabled))
            return;
        if (!await DisplayAlertAsync("Start continuous location sharing?",
            _hasViewerGrant
                ? "FamilyOS will update your location about once a minute while this Android foreground service is running. A persistent notification will remain visible, and you can stop tracking at any time. Your location is visible only to household members you explicitly allowed."
                : "FamilyOS will record your location history about once a minute while this Android foreground service is running. No household member can see your location unless you separately grant access. A persistent notification will remain visible, and you can stop tracking at any time.",
            "Start tracking", "Cancel")) return;

#if ANDROID
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var notificationPermission = await Permissions.RequestAsync<Permissions.PostNotifications>();
                if (notificationPermission != PermissionStatus.Granted)
                {
                    _trackingStatus.Text = "Notification permission is required so tracking remains visible. Enable notifications in Android settings and try again.";
                    return;
                }
            }
            FamilyOS.Maui.Platforms.Android.LocationTrackingController.Start();
            _trackingStatus.Text = "Starting continuous tracking…";
            RefreshState();
        }
        catch (Exception ex)
        {
            _trackingStatus.Text = $"Could not start location tracking: {ex.Message}";
        }
#else
        _trackingStatus.Text = "Continuous tracking is currently supported on Android only.";
#endif
    }

    private void StopTracking()
    {
#if ANDROID
        try
        {
            FamilyOS.Maui.Platforms.Android.LocationTrackingController.Stop();
            _trackingStatus.Text = "Tracking stopped. No further updates should be sent.";
            RefreshState();
        }
        catch (Exception ex)
        {
            _trackingStatus.Text = $"Could not stop location tracking: {ex.Message}";
        }
#else
        _trackingStatus.Text = "Continuous tracking is currently supported on Android only.";
#endif
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _enable.IsEnabled = !busy;
        RefreshState();
    }
}
