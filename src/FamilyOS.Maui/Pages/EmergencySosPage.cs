using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Explicit household emergency alert. Location is optional and is only
/// acquired for this single alert after a separate user confirmation.
/// </summary>
public sealed class EmergencySosPage : ContentPage
{
    private readonly FamilyApiClient _api;
    private readonly DeviceLocationService _deviceLocation;
    private readonly Entry _message;
    private readonly Button _send;
    private readonly Label _status;
    private bool _sending;

    public EmergencySosPage(FamilyApiClient api, DeviceLocationService deviceLocation)
    {
        _api = api;
        _deviceLocation = deviceLocation;
        Title = "Emergency SOS";

        var heading = new Label
        {
            Text = "Emergency SOS",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");

        var explanation = new Label
        {
            Text = "Send an urgent alert to every other active member of your household. You choose whether to include a one-time location link. SOS does not contact 911 or emergency services.",
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };
        explanation.SetDynamicResource(VisualElement.StyleProperty, "TextBody");

        _message = new Entry
        {
            Placeholder = "Optional message (up to 500 characters)",
            MaxLength = 500
        };
        _status = new Label
        {
            Text = "No alert has been sent.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        _send = new Button
        {
            Text = "Send emergency SOS",
            BackgroundColor = Color.FromArgb("#B91C1C"),
            TextColor = Colors.White
        };
        _send.Clicked += async (_, _) => await SendAsync();

        var note = new Label
        {
            Text = "Location is not included by default. If you choose to include it, FamilyOS explains the request and asks Android for location permission only if needed. The location is attached to this alert only; it does not enable ongoing sharing or history.",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        };
        note.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var card = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children = { heading, explanation, _message, _send, _status, note }
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
    }

    private async Task SendAsync()
    {
        if (_sending) return;

        var choice = await DisplayActionSheetAsync(
            "Include your current location with this one-time alert?",
            "Cancel", null,
            "Send without location",
            "Send with current location");
        if (choice == "Cancel" || string.IsNullOrWhiteSpace(choice))
            return;

        double? latitude = null;
        double? longitude = null;
        var includeLocation = choice == "Send with current location";

        if (includeLocation)
        {
            var permissionExplained = await DisplayAlertAsync(
                "Share a one-time location?",
                "FamilyOS will request foreground location access if needed and attach your current position to this SOS alert for household members. This does not enable continuous tracking or location history.",
                "Continue", "Cancel");
            if (!permissionExplained) return;

            try
            {
                if (!_deviceLocation.IsEnabled && !await _deviceLocation.EnableAsync())
                {
                    var sendWithout = await DisplayAlertAsync(
                        "Location permission not granted",
                        "You can still send an SOS alert without location. No location will be attached.",
                        "Send without location", "Cancel");
                    if (!sendWithout) return;
                }
                else
                {
                    var fix = await _deviceLocation.GetCurrentLocationAsync();
                    if (fix is null)
                    {
                        var sendWithout = await DisplayAlertAsync(
                            "Location unavailable",
                            "FamilyOS could not get a current location fix. Send the SOS without location?",
                            "Send without location", "Cancel");
                        if (!sendWithout) return;
                    }
                    else
                    {
                        latitude = fix.Latitude;
                        longitude = fix.Longitude;
                    }
                }
            }
            catch (Exception)
            {
                var sendWithout = await DisplayAlertAsync(
                    "Location unavailable",
                    "FamilyOS could not access a current location. Send the SOS without location?",
                    "Send without location", "Cancel");
                if (!sendWithout) return;
            }
        }

        var confirmText = latitude.HasValue
            ? "This will send an emergency alert and a one-time OpenStreetMap location link to every other active household member. FamilyOS does not contact 911."
            : "This will send an emergency alert without location to every other active household member. FamilyOS does not contact 911.";
        if (!await DisplayAlertAsync("Confirm emergency SOS", confirmText, "Send SOS", "Cancel"))
            return;

        try
        {
            _sending = true;
            _send.IsEnabled = false;
            _status.Text = "Sending emergency alert…";
            var result = await _api.SendSosAsync(_message.Text, latitude, longitude);
            _status.Text = $"SOS alert sent to {result.AlertsSent} household member(s)."
                + (result.LocationIncluded ? " A one-time location link was included." : " No location was included.");
            _message.Text = string.Empty;
        }
        catch (FamilyApiException ex)
        {
            _status.Text = $"Could not send SOS: {ex.Message}";
        }
        catch (Exception)
        {
            _status.Text = "Could not send SOS. Check your connection and try again.";
        }
        finally
        {
            _sending = false;
            _send.IsEnabled = true;
        }
    }
}
