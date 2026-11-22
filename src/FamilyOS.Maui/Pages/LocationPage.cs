using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// First location milestone: explicit device consent and a user-initiated
/// foreground fix. Household sharing is intentionally not implied or enabled.
/// </summary>
public sealed class LocationPage : ContentPage
{
    private readonly DeviceLocationService _location;
    private readonly Label _consentStatus;
    private readonly Label _fixStatus;
    private readonly Button _enable;
    private readonly Button _update;
    private readonly Button _disable;
    private Location? _lastLocation;
    private bool _busy;

    public LocationPage(DeviceLocationService location)
    {
        _location = location;
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
            Text = "Location access is optional. FamilyOS only checks your location when you ask it to on this screen. This first release does not run background tracking or share coordinates with household members.",
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

        var privacy = new Label
        {
            Text = "Privacy: permission and consent are stored on this device. Coordinates are held in memory only for this screen session; they are not sent to FamilyOS servers.",
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
                Children = { heading, explanation, _consentStatus, _enable, _update, _disable, _fixStatus, privacy }
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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshState();
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
    }

    private async Task DisableAsync()
    {
        if (_busy) return;
        if (!await DisplayAlertAsync("Turn off location access?",
            "FamilyOS will stop requesting your device location. Android permission can also be revoked in app settings.",
            "Turn off", "Cancel")) return;

        _location.Disable();
        _lastLocation = null;
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
