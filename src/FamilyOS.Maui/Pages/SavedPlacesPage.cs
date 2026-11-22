using System.Globalization;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Manage household geofences used for arrival and departure notifications.</summary>
public sealed class SavedPlacesPage : ContentPage
{
    private readonly FamilyApiClient _api;
    private readonly DeviceLocationService _location;
    private readonly VerticalStackLayout _places;
    private readonly Label _status;
    private readonly Button _add;
    private bool _busy;

    public SavedPlacesPage(FamilyApiClient api, DeviceLocationService location)
    {
        _api = api;
        _location = location;
        Title = "Saved places";

        var heading = new Label { Text = "Family places", FontSize = 25, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var explanation = new Label
        {
            Text = "Save places such as Home, School, or Work. When a household member shares location and has explicitly granted you access, FamilyOS can notify you when they arrive or leave.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        explanation.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _status = new Label { Text = "Loading saved places…", FontSize = 13 };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _add = new Button { Text = "Add place at my current location" };
        _add.Clicked += async (_, _) => await AddPlaceAsync();
        var refresh = new Button { Text = "Refresh places", FontSize = 13 };
        refresh.Clicked += async (_, _) => await LoadAsync();
        _places = new VerticalStackLayout { Spacing = 10 };

        var card = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children = { heading, explanation, _status, _add, refresh, _places }
            }
        };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 14),
                Spacing = 12,
                Children = { card }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        _busy = true;
        _add.IsEnabled = false;
        _status.Text = "Loading household places…";
        try
        {
            var places = await _api.GetSavedPlacesAsync();
            _places.Children.Clear();
            if (places.Count == 0)
                _status.Text = "No saved places yet. Add one at your current location.";
            else
                _status.Text = $"{places.Count} saved place(s). Geofence alerts use a radius around each saved location.";

            foreach (var place in places)
            {
                var details = new Label
                {
                    Text = $"{place.Name}\nRadius: {place.RadiusMeters:0} m · Arrival alerts: {(place.AlertOnArrival ? "On" : "Off")} · Departure alerts: {(place.AlertOnDeparture ? "On" : "Off")}",
                    FontSize = 13,
                    LineBreakMode = LineBreakMode.WordWrap
                };
                details.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
                var coordinates = new Label
                {
                    Text = $"{place.Latitude:F5}, {place.Longitude:F5}",
                    FontSize = 11
                };
                coordinates.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
                var row = new HorizontalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center };
                var labels = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.FillAndExpand, Children = { details, coordinates } };
                row.Children.Add(labels);
                if (place.CanEdit)
                {
                    var delete = new Button { Text = "Delete", FontSize = 12, Padding = new Thickness(10, 6) };
                    delete.Clicked += async (_, _) => await DeletePlaceAsync(place);
                    row.Children.Add(delete);
                }
                var item = new Border
                {
                    Padding = 12,
                    Content = row
                };
                item.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
                _places.Children.Add(item);
            }
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not load saved places: {ex.Message}";
        }
        finally
        {
            _busy = false;
            _add.IsEnabled = true;
        }
    }

    private async Task AddPlaceAsync()
    {
        if (_busy) return;
        if (!_location.IsEnabled)
        {
            await DisplayAlertAsync("Location is off",
                "Enable device location on the Location page before adding a place at your current position.",
                "OK");
            return;
        }

        var name = await DisplayPromptAsync("New family place", "Enter a name such as Home, School, or Work.",
            "Continue", "Cancel", "Place name", 120);
        if (string.IsNullOrWhiteSpace(name)) return;

        var radiusText = await DisplayPromptAsync("Geofence radius",
            "Choose a boundary radius in meters (50–5,000).", "Continue", "Cancel",
            "150", 5, Keyboard.Numeric, "150");
        if (radiusText is null) return;
        if (!double.TryParse(radiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius)
            || !double.IsFinite(radius) || radius is < 50 or > 5000)
        {
            await DisplayAlertAsync("Invalid radius", "Enter a number between 50 and 5,000 meters.", "OK");
            return;
        }

        var arrival = await DisplayAlertAsync("Arrival notifications",
            "Notify household members who have permission to view a member's location when that member arrives here?",
            "Yes", "No");
        var departure = await DisplayAlertAsync("Departure notifications",
            "Notify permitted household viewers when that member leaves this place?",
            "Yes", "No");

        _busy = true;
        _add.IsEnabled = false;
        _status.Text = "Getting your current location…";
        try
        {
            var fix = await _location.GetCurrentLocationAsync();
            if (fix is null)
            {
                _status.Text = "Android could not determine your current location.";
                return;
            }
            await _api.CreateSavedPlaceAsync(name.Trim(), fix.Latitude, fix.Longitude,
                radius, arrival, departure);
            _status.Text = $"Saved {name.Trim()}.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not create saved place: {ex.Message}";
        }
        finally
        {
            _busy = false;
            _add.IsEnabled = true;
        }
    }

    private async Task DeletePlaceAsync(SavedPlaceDto place)
    {
        if (!await DisplayAlertAsync("Delete saved place?",
            $"Delete {place.Name} and stop its arrival/departure alerts?",
            "Delete", "Cancel")) return;
        try
        {
            await _api.DeleteSavedPlaceAsync(place.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not delete {place.Name}: {ex.Message}";
        }
    }
}
