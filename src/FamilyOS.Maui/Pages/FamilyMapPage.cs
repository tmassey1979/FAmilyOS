using System.Globalization;
using System.Text.Json;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Full-screen household map with explicit location sharing controls.
/// Sharing is opt-in; coordinates only leave the device when the user shares.
/// Google Maps dark mode (API key in Preferences); labeled place markers.
/// </summary>
public sealed class FamilyMapPage : ContentPage, IQueryAttributable
{
    public const string GoogleMapsApiKeyPreference = "familyos.maps.google_api_key";

    private readonly FamilyApiClient _api;
    private readonly DeviceLocationService _deviceLocation;
    private Location? _ownLocation;
    private DateTimeOffset _ownLocationReadAtUtc;
    private readonly WebView _map = new()
    {
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill
    };
    private readonly Label _status = new()
    {
        FontSize = 12,
        LineBreakMode = LineBreakMode.WordWrap,
        HorizontalTextAlignment = TextAlignment.Center
    };
    private readonly Button _locate = new() { Text = "\u2316", FontSize = 20, WidthRequest = 48, HeightRequest = 48 };
    private readonly Button _refresh = new() { Text = "\u21BB", FontSize = 20, WidthRequest = 48, HeightRequest = 48 };
    private readonly Picker _historyMember = new()
    {
        Title = "Member",
        ItemDisplayBinding = new Binding(nameof(FamilyMemberDto.DisplayName)),
        FontSize = 13
    };
    private readonly Button _showHistory = new() { Text = "24h", FontSize = 13, IsEnabled = false };
    private readonly Button _showTrips = new() { Text = "Trips", FontSize = 13, IsEnabled = false };
    private readonly Label _historyStatus = new() { FontSize = 11, LineBreakMode = LineBreakMode.WordWrap, IsVisible = false };
    private FamilyDto? _family;
    private List<FamilyLocationDto> _currentLocations = [];
    private List<LocationHistoryPointDto> _visibleHistory = [];
    private bool _historyViewActive;
    private Guid? _historyTargetMemberId;
    private bool _loading;
    private bool _isVisible;
    private bool _timerStarted;
    private bool _autoLocated;
    private Guid? _requestedMemberId;
    private bool _sharingEnabled;
    private bool _loadingShareToggle;
    private readonly Switch _shareToggle = new() { HorizontalOptions = LayoutOptions.Start };
    private readonly Label _shareLabel = new() { FontSize = 13, VerticalOptions = LayoutOptions.Center };
    private readonly Button _shareNow = new() { Text = "Share now", FontSize = 13 };
    private readonly Button _placesBtn = new() { Text = "Places", FontSize = 13 };
    private readonly Button _locationSettings = new() { Text = "Settings", FontSize = 13 };
    private readonly Button _mapsKeyBtn = new() { Text = "Maps key", FontSize = 13 };

    public FamilyMapPage(FamilyApiClient api, DeviceLocationService deviceLocation)
    {
        _api = api;
        _deviceLocation = deviceLocation;
        Title = "Map";

        _status.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        _status.Text = "Finding your location\u2026";

        foreach (var fab in new[] { _locate, _refresh })
        {
            fab.BackgroundColor = Color.FromArgb("#121A2B");
            fab.TextColor = Color.FromArgb("#22D3EE");
            fab.CornerRadius = 14;
            fab.BorderColor = Color.FromArgb("#22D3EE");
            fab.BorderWidth = 1.5;
            fab.Padding = 0;
            fab.FontAttributes = FontAttributes.Bold;
        }

        _showHistory.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _showTrips.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _shareNow.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _placesBtn.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _locationSettings.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _mapsKeyBtn.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _shareLabel.Text = "Sharing: \u2026";

        _locate.Clicked += async (_, _) => await EnsureOwnLocationAsync(forcePrompt: true);
        _refresh.Clicked += async (_, _) => await RefreshAsync();
        _historyMember.SelectedIndexChanged += (_, _) =>
        {
            var selectedMember = _historyMember.SelectedItem as FamilyMemberDto;
            _showHistory.IsEnabled = selectedMember is not null;
            _showTrips.IsEnabled = selectedMember is not null;

            if (_historyViewActive && selectedMember?.Id != _historyTargetMemberId)
            {
                _visibleHistory = [];
                _historyViewActive = false;
                _historyTargetMemberId = null;
                _historyStatus.IsVisible = false;
                if (!_loading)
                    _ = RefreshAsync();
            }
        };
        _showHistory.Clicked += async (_, _) => await ShowHistoryAsync();
        _showTrips.Clicked += async (_, _) => await ShowTripsAsync();
        _shareToggle.Toggled += async (_, e) =>
        {
            if (!_loadingShareToggle)
                await SetSharingAsync(e.Value);
        };
        _shareNow.Clicked += async (_, _) => await ShareNowAsync();
        _placesBtn.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(SavedPlacesPage));
        _locationSettings.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(LocationPage));
        _mapsKeyBtn.Clicked += async (_, _) => await EditGoogleMapsApiKeyAsync();

        var statusChip = new Border
        {
            Padding = new Thickness(12, 8),
            StrokeThickness = 0,
            Content = _status,
            HorizontalOptions = LayoutOptions.Fill
        };
        statusChip.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        statusChip.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 };

        var shareBar = new Border
        {
            Padding = new Thickness(12, 8),
            StrokeThickness = 0,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children = { _shareToggle, _shareLabel }
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { _shareNow, _placesBtn, _locationSettings, _mapsKeyBtn }
                    }
                }
            }
        };
        shareBar.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        shareBar.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 };

        var topOverlay = new VerticalStackLayout
        {
            Padding = new Thickness(12, 8, 12, 0),
            Spacing = 6,
            VerticalOptions = LayoutOptions.Start,
            Children = { statusChip, shareBar }
        };

        var fabColumn = new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Children = { _locate, _refresh }
        };

        var bottomBar = new Border
        {
            Padding = new Thickness(12, 10),
            StrokeThickness = 0,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { _historyMember, _showHistory, _showTrips }
                    },
                    _historyStatus
                }
            }
        };
        bottomBar.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        bottomBar.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 };

        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star) } };
        root.Add(_map, 0, 0);
        root.Add(topOverlay, 0, 0);
        root.Add(fabColumn, 0, 0);
        var bottomHost = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.End,
            Padding = new Thickness(12, 0, 72, 12),
            Children = { bottomBar }
        };
        root.Add(bottomHost, 0, 0);

        Content = root;
        _map.Source = new HtmlWebViewSource { Html = BuildMapHtml([], []) };
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("memberId", out var raw) && Guid.TryParse(raw?.ToString(), out var memberId))
        {
            _requestedMemberId = memberId;
            if (_family is not null)
                _historyMember.SelectedItem = _family.Members.FirstOrDefault(x => x.Id == memberId);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isVisible = true;
        if (!_timerStarted)
        {
            _timerStarted = true;
            Dispatcher.StartTimer(TimeSpan.FromSeconds(30), () =>
            {
                if (!_isVisible)
                {
                    _timerStarted = false;
                    return false;
                }
                _ = RefreshAsync();
                return true;
            });
        }

        await LoadFamilyMembersAsync();
        await LoadSharingStateAsync();
        if (!_autoLocated)
        {
            _autoLocated = true;
            await EnsureOwnLocationAsync(forcePrompt: false);
        }
        await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        _isVisible = false;
        base.OnDisappearing();
    }

    private async Task LoadSharingStateAsync()
    {
        try
        {
            var consent = await _api.GetLocationConsentAsync();
            _sharingEnabled = consent.SharingEnabled;
            _loadingShareToggle = true;
            _shareToggle.IsToggled = _sharingEnabled;
            _loadingShareToggle = false;
            UpdateShareUi();
        }
        catch (Exception ex)
        {
            _shareLabel.Text = $"Sharing: unavailable ({ex.Message})";
            _shareNow.IsEnabled = false;
        }
    }

    private void UpdateShareUi()
    {
        _shareLabel.Text = _sharingEnabled
            ? "Sharing on \u2014 household can see your pin when you share"
            : "Sharing off \u2014 only you see your location on this device";
        _shareNow.IsEnabled = _sharingEnabled && _deviceLocation.IsEnabled;
        _shareToggle.IsEnabled = true;
    }

    private async Task SetSharingAsync(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var ok = await DisplayAlert(
                    "Share location with household?",
                    "Only people you allow in Location settings can see coordinates you share. Nothing is sent until you tap Share now or start continuous tracking.",
                    "Turn on sharing",
                    "Cancel");
                if (!ok)
                {
                    _loadingShareToggle = true;
                    _shareToggle.IsToggled = false;
                    _loadingShareToggle = false;
                    return;
                }
                if (!_deviceLocation.IsEnabled)
                {
                    if (!await _deviceLocation.EnableAsync())
                    {
                        await DisplayAlert("Permission needed", "Allow location access on this device to share.", "OK");
                        _loadingShareToggle = true;
                        _shareToggle.IsToggled = false;
                        _loadingShareToggle = false;
                        return;
                    }
                }
            }
            else
            {
                var ok = await DisplayAlert(
                    "Stop sharing?",
                    "Your last shared coordinates will be removed from the household map.",
                    "Stop sharing",
                    "Cancel");
                if (!ok)
                {
                    _loadingShareToggle = true;
                    _shareToggle.IsToggled = true;
                    _loadingShareToggle = false;
                    return;
                }
            }

            var result = await _api.SetLocationSharingAsync(enabled);
            _sharingEnabled = result.SharingEnabled;
            _loadingShareToggle = true;
            _shareToggle.IsToggled = _sharingEnabled;
            _loadingShareToggle = false;
            UpdateShareUi();
            _status.Text = _sharingEnabled
                ? "Sharing is on. Tap Share now to publish your current position."
                : "Sharing is off. Your pin is only local until you turn it on again.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _loadingShareToggle = true;
            _shareToggle.IsToggled = _sharingEnabled;
            _loadingShareToggle = false;
            _status.Text = $"Could not update sharing: {ex.Message}";
            UpdateShareUi();
        }
    }

    private async Task ShareNowAsync()
    {
        if (!_sharingEnabled)
        {
            _status.Text = "Turn sharing on first.";
            return;
        }
        try
        {
            _shareNow.IsEnabled = false;
            if (!_deviceLocation.IsEnabled && !await _deviceLocation.EnableAsync())
            {
                _status.Text = "Location permission is required to share.";
                return;
            }

            _status.Text = "Getting your location\u2026";
            _ownLocation = await _deviceLocation.GetCurrentLocationAsync();
            _ownLocationReadAtUtc = DateTimeOffset.UtcNow;
            if (_ownLocation is null)
            {
                _status.Text = "Could not read a GPS fix.";
                return;
            }

            await _api.ShareLocationAsync(
                _ownLocation.Latitude,
                _ownLocation.Longitude,
                _ownLocation.Accuracy ?? 0,
                _ownLocation.Timestamp.UtcDateTime);
            _status.Text = $"Shared {DateTime.Now:t} \u00B7 {_ownLocation.Latitude:F5}, {_ownLocation.Longitude:F5}";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"Share failed: {ex.Message}";
        }
        finally
        {
            UpdateShareUi();
        }
    }

    private async Task EnsureOwnLocationAsync(bool forcePrompt)
    {
        try
        {
            _locate.IsEnabled = false;
            if (!_deviceLocation.IsEnabled || forcePrompt)
            {
                if (!await _deviceLocation.EnableAsync())
                {
                    _status.Text = "Location permission off \u2014 shared household pins still load.";
                    return;
                }
            }

            _status.Text = "Getting your location\u2026";
            _ownLocation = await _deviceLocation.GetCurrentLocationAsync();
            _ownLocationReadAtUtc = DateTimeOffset.UtcNow;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not get your location: {ex.Message}";
        }
        finally
        {
            _locate.IsEnabled = true;
        }
    }

    private async Task LoadFamilyMembersAsync()
    {
        try
        {
            _family = await _api.GetFamilyAsync();
            _historyMember.ItemsSource = _family.Members
                .Where(x => x.IsActive)
                .ToList();
            _historyMember.SelectedItem = _family.Members.FirstOrDefault(x => x.Id == (_requestedMemberId ?? _family.CurrentMemberId));
            _requestedMemberId = null;
            var selected = _historyMember.SelectedItem is FamilyMemberDto;
            _showHistory.IsEnabled = selected;
            _showTrips.IsEnabled = selected;
        }
        catch (Exception ex)
        {
            _historyStatus.IsVisible = true;
            _historyStatus.Text = $"Household: {ex.Message}";
        }
    }

    private async Task ShowHistoryAsync()
    {
        if (_historyMember.SelectedItem is not FamilyMemberDto member) return;
        try
        {
            var from = DateTimeOffset.UtcNow.AddHours(-24);
            var history = await _api.GetLocationHistoryAsync(member.Id, from);
            var places = await _api.GetSavedPlacesAsync();
            _visibleHistory = history;
            _historyTargetMemberId = member.Id;
            _historyViewActive = true;
            _map.Source = new HtmlWebViewSource { Html = BuildMapHtml(_currentLocations, places, _visibleHistory, member.Id) };
            _historyStatus.IsVisible = true;
            _historyStatus.Text = history.Count < 2
                ? $"{member.DisplayName}: not enough points in the last 24 hours."
                : $"{member.DisplayName}: {history.Count} points \u00B7 last 24 hours.";
        }
        catch (Exception ex)
        {
            _historyStatus.IsVisible = true;
            _historyStatus.Text = $"History unavailable: {ex.Message}";
        }
    }

    private async Task ShowTripsAsync()
    {
        if (_historyMember.SelectedItem is not FamilyMemberDto member) return;
        try
        {
            var trips = await _api.GetLocationTripsAsync(member.Id, DateTimeOffset.UtcNow.AddHours(-24));
            _historyStatus.IsVisible = true;
            _historyStatus.Text = trips.Count == 0
                ? $"{member.DisplayName}: no trips in the last 24 hours."
                : string.Join(" \u00B7 ", trips.Select((trip, index) =>
                    $"#{index + 1} {trip.DistanceMeters / 1000:0.0} km"));
        }
        catch (Exception ex)
        {
            _historyStatus.IsVisible = true;
            _historyStatus.Text = $"Trips unavailable: {ex.Message}";
        }
    }

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        _refresh.IsEnabled = false;
        try
        {
            var locations = await _api.GetFamilyLocationsAsync();
            _currentLocations = locations;
            var places = await _api.GetSavedPlacesAsync();
            var valid = locations
                .Where(x => x.Latitude is >= -90 and <= 90
                    && x.Longitude is >= -180 and <= 180
                    && x.UpdatedAtUtc.HasValue)
                .ToList();

            if (_deviceLocation.IsEnabled && (_ownLocation is null ||
                DateTimeOffset.UtcNow - _ownLocationReadAtUtc > TimeSpan.FromMinutes(2)))
            {
                try
                {
                    _ownLocation = await _deviceLocation.GetCurrentLocationAsync();
                    _ownLocationReadAtUtc = DateTimeOffset.UtcNow;
                }
                catch { /* keep last known */ }
            }

            if (_ownLocation is not null && _family is not null)
            {
                var ownMember = _family.Members.FirstOrDefault(x => x.Id == _family.CurrentMemberId);
                if (ownMember is not null)
                {
                    valid.RemoveAll(x => x.MemberId == ownMember.Id);
                    valid.Add(new FamilyLocationDto(
                        ownMember.Id,
                        ownMember.DisplayName,
                        _ownLocation.Latitude,
                        _ownLocation.Longitude,
                        _ownLocation.Accuracy,
                        _ownLocationReadAtUtc.UtcDateTime));
                }
            }

            _map.Source = new HtmlWebViewSource
            {
                Html = BuildMapHtml(valid, places, _historyViewActive ? _visibleHistory : null, _requestedMemberId ?? (_historyMember.SelectedItem as FamilyMemberDto)?.Id)
            };

            var ownVisible = _ownLocation is not null;
            if (valid.Count == 0)
                _status.Text = ownVisible ? "Only you on the map \u2014 share to include household pins." : "No shared locations yet.";
            else if (ownVisible)
                _status.Text = $"You \u00B7 {Math.Max(0, valid.Count - 1)} shared \u00B7 updated {DateTime.Now:t}";
            else
                _status.Text = $"{valid.Count} shared \u00B7 latest {valid.Max(x => x.UpdatedAtUtc!.Value).ToLocalTime():t}";
        }
        catch (Exception ex)
        {
            _status.Text = $"Map update failed: {ex.Message}";
        }
        finally
        {
            _loading = false;
            _refresh.IsEnabled = true;
        }
    }

    private async Task EditGoogleMapsApiKeyAsync()
    {
        var current = Preferences.Default.Get(GoogleMapsApiKeyPreference, string.Empty);
        var value = await DisplayPromptAsync(
            "Google Maps API key",
            "Paste a Maps JavaScript API key from Google Cloud (enable Maps JavaScript API + billing). Stored only on this device.",
            accept: "Save",
            cancel: "Cancel",
            placeholder: "AIza...",
            initialValue: current,
            maxLength: 200);
        if (value is null) return;
        Preferences.Default.Set(GoogleMapsApiKeyPreference, value.Trim());
        _status.Text = string.IsNullOrWhiteSpace(value.Trim())
            ? "Google Maps key cleared."
            : "Google Maps key saved on this device.";
        await RefreshAsync();
    }

    private static string BuildMapHtml(IReadOnlyList<FamilyLocationDto> locations, IReadOnlyList<SavedPlaceDto> places, IReadOnlyList<LocationHistoryPointDto>? history = null, Guid? selectedMemberId = null)
    {
        var apiKey = Preferences.Default.Get(GoogleMapsApiKeyPreference, string.Empty)?.Trim() ?? string.Empty;
        var data = locations.Select(x => new MapPin(
            x.MemberId,
            x.DisplayName,
            x.Latitude!.Value,
            x.Longitude!.Value,
            x.AccuracyMeters,
            x.UpdatedAtUtc!.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var placeJson = JsonSerializer.Serialize(places, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var historyJson = JsonSerializer.Serialize(history ?? [], new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return """
            <!doctype html>
            <html><head>
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                html,body{height:100%;margin:0;background:#0A0E14;color:#E2E8F0;font-family:-apple-system,system-ui,sans-serif}
                .box{display:flex;align-items:center;justify-content:center;height:100%;padding:28px;text-align:center;line-height:1.5}
                strong{color:#22D3EE}
              </style>
            </head><body>
              <div class="box">
                <div>
                  <div style="font-size:18px;font-weight:700;margin-bottom:8px">Google Maps dark mode</div>
                  <div style="color:#94A3B8;font-size:14px">
                    Tap <strong>Maps key</strong> on this screen, paste a Google Cloud
                    <strong>Maps JavaScript API</strong> key, then Save.
                    Enable the API and billing on the key in Google Cloud Console.
                  </div>
                </div>
              </div>
            </body></html>
            """;
        }

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
              <style>
                html, body, #map { height: 100%; width: 100%; margin: 0; padding: 0; background: #0A0E14; }
              </style>
            </head>
            <body>
              <div id="map"></div>
              <script>
                const locations = {{json}};
                const places = {{placeJson}};
                const history = {{historyJson}};
                const selectedMemberId = {{JsonSerializer.Serialize(selectedMemberId?.ToString())}};

                const darkStyles = [
                  { elementType: "geometry", stylers: [{ color: "#1d2c4d" }] },
                  { elementType: "labels.text.fill", stylers: [{ color: "#8ec3b9" }] },
                  { elementType: "labels.text.stroke", stylers: [{ color: "#1a3646" }] },
                  { featureType: "administrative.country", elementType: "geometry.stroke", stylers: [{ color: "#4b6878" }] },
                  { featureType: "administrative.land_parcel", elementType: "labels.text.fill", stylers: [{ color: "#64779e" }] },
                  { featureType: "administrative.province", elementType: "geometry.stroke", stylers: [{ color: "#4b6878" }] },
                  { featureType: "landscape.man_made", elementType: "geometry.stroke", stylers: [{ color: "#334e87" }] },
                  { featureType: "landscape.natural", elementType: "geometry", stylers: [{ color: "#023e58" }] },
                  { featureType: "poi", elementType: "geometry", stylers: [{ color: "#283d6a" }] },
                  { featureType: "poi", elementType: "labels.text.fill", stylers: [{ color: "#6f9ba5" }] },
                  { featureType: "poi", elementType: "labels.text.stroke", stylers: [{ color: "#1d2c4d" }] },
                  { featureType: "poi.park", elementType: "geometry.fill", stylers: [{ color: "#023e58" }] },
                  { featureType: "poi.park", elementType: "labels.text.fill", stylers: [{ color: "#3C7680" }] },
                  { featureType: "road", elementType: "geometry", stylers: [{ color: "#304a7d" }] },
                  { featureType: "road", elementType: "labels.text.fill", stylers: [{ color: "#98a5be" }] },
                  { featureType: "road", elementType: "labels.text.stroke", stylers: [{ color: "#1d2c4d" }] },
                  { featureType: "road.highway", elementType: "geometry", stylers: [{ color: "#2c6675" }] },
                  { featureType: "road.highway", elementType: "geometry.stroke", stylers: [{ color: "#255763" }] },
                  { featureType: "road.highway", elementType: "labels.text.fill", stylers: [{ color: "#b0d5ce" }] },
                  { featureType: "road.highway", elementType: "labels.text.stroke", stylers: [{ color: "#023e58" }] },
                  { featureType: "transit", elementType: "labels.text.fill", stylers: [{ color: "#98a5be" }] },
                  { featureType: "transit", elementType: "labels.text.stroke", stylers: [{ color: "#1d2c4d" }] },
                  { featureType: "transit.line", elementType: "geometry.fill", stylers: [{ color: "#283d6a" }] },
                  { featureType: "transit.station", elementType: "geometry", stylers: [{ color: "#3a4762" }] },
                  { featureType: "water", elementType: "geometry", stylers: [{ color: "#0e1626" }] },
                  { featureType: "water", elementType: "labels.text.fill", stylers: [{ color: "#4e6d70" }] }
                ];

                function initMap() {
                  const map = new google.maps.Map(document.getElementById('map'), {
                    center: { lat: 39.5, lng: -98.35 },
                    zoom: 4,
                    styles: darkStyles,
                    disableDefaultUI: false,
                    zoomControl: true,
                    mapTypeControl: false,
                    streetViewControl: false,
                    fullscreenControl: false,
                    backgroundColor: '#0A0E14'
                  });

                  const bounds = new google.maps.LatLngBounds();
                  let hasBounds = false;

                  if (history && history.length > 1) {
                    const path = history.map(p => ({ lat: p.latitude, lng: p.longitude }));
                    path.forEach(p => { bounds.extend(p); hasBounds = true; });
                    new google.maps.Polyline({
                      path,
                      geodesic: true,
                      strokeColor: '#22D3EE',
                      strokeOpacity: 0.9,
                      strokeWeight: 4,
                      map
                    });
                  }

                  for (const place of places) {
                    const pos = { lat: place.latitude, lng: place.longitude };
                    bounds.extend(pos); hasBounds = true;
                    new google.maps.Circle({
                      strokeColor: '#22D3EE',
                      strokeOpacity: 0.9,
                      strokeWeight: 2,
                      fillColor: '#22D3EE',
                      fillOpacity: 0.12,
                      map,
                      center: pos,
                      radius: place.radiusMeters || 50
                    });
                    const alerts = [];
                    if (place.alertOnArrival) alerts.push('Arrive');
                    if (place.alertOnDeparture) alerts.push('Leave');
                    const title = place.name || 'Saved place';
                    const marker = new google.maps.Marker({
                      position: pos,
                      map,
                      title,
                      label: {
                        text: title.length > 18 ? title.slice(0, 16) + '\u2026' : title,
                        color: '#E2E8F0',
                        fontSize: '11px',
                        fontWeight: '600'
                      }
                    });
                    const info = new google.maps.InfoWindow({
                      content: '<div style="color:#0f172a"><strong>' + title +
                        '</strong><div>Place \u00B7 ' + Math.round(place.radiusMeters || 0) + ' m' +
                        (alerts.length ? ' \u00B7 ' + alerts.join('/') : '') + '</div></div>'
                    });
                    marker.addListener('click', () => info.open({ map, anchor: marker }));
                  }

                  for (const item of locations) {
                    const pos = { lat: item.latitude, lng: item.longitude };
                    bounds.extend(pos); hasBounds = true;
                    const ageMinutes = (Date.now() - new Date(item.updatedAtUtc).getTime()) / 60000;
                    const freshness = ageMinutes < 2 ? 'Live' : ageMinutes < 15 ? 'Recent' : ageMinutes < 60 ? 'Stale' : 'Old';
                    const marker = new google.maps.Marker({
                      position: pos,
                      map,
                      title: item.displayName || 'Member'
                    });
                    const info = new google.maps.InfoWindow({
                      content: '<div style="color:#0f172a"><strong>' + (item.displayName || 'Member') +
                        '</strong><div>' + freshness + ' \u00B7 ' + new Date(item.updatedAtUtc).toLocaleString() + '</div></div>'
                    });
                    marker.addListener('click', () => info.open({ map, anchor: marker }));
                    if (selectedMemberId && item.memberId === selectedMemberId) info.open({ map, anchor: marker });
                  }

                  if (hasBounds) map.fitBounds(bounds, 48);
                }

                window.initMap = initMap;
              </script>
              <script async defer
                src="https://maps.googleapis.com/maps/api/js?key={{apiKey}}&callback=initMap">
              </script>
            </body>
            </html>
            """;
    }

    private sealed record MapPin(Guid MemberId, string DisplayName, double Latitude, double Longitude, double? AccuracyMeters, string UpdatedAtUtc);
}
