using System.Globalization;
using System.Text.Json;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Displays only the latest locations explicitly shared by active household members.
/// Map tiles are provided by OpenStreetMap with visible attribution.
/// </summary>
public sealed class FamilyMapPage : ContentPage
{
    private readonly FamilyApiClient _api;
    private readonly DeviceLocationService _deviceLocation;
    private Location? _ownLocation;
    private DateTimeOffset _ownLocationReadAtUtc;
    private readonly WebView _map;
    private readonly Label _status;
    private readonly Button _refresh;
    private readonly Button _showMyLocation;
    private readonly Picker _historyMember;
    private readonly Label _historyStatus;
    private readonly Button _showHistory;
    private readonly Button _showTrips;
    private FamilyDto? _family;
    private List<FamilyLocationDto> _currentLocations = [];
    private bool _loading;
    private bool _isVisible;
    private bool _timerStarted;

    public FamilyMapPage(FamilyApiClient api, DeviceLocationService deviceLocation)
    {
        _api = api;
        _deviceLocation = deviceLocation;
        Title = "Family map";

        var heading = new Label
        {
            Text = "Household map",
            FontSize = 25,
            FontAttributes = FontAttributes.Bold
        };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");

        var explanation = new Label
        {
            Text = "Your own device location can appear locally when device location is enabled. Other household pins appear only when those members explicitly share with you. Map tiles are provided by OpenStreetMap.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        explanation.SetDynamicResource(VisualElement.StyleProperty, "TextBody");

        _status = new Label
        {
            Text = "Load shared household locations to see the map.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        _refresh = new Button { Text = "Refresh household map" };
        _refresh.Clicked += async (_, _) => await RefreshAsync();
        _showMyLocation = new Button { Text = "Show my location on this map" };
        _showMyLocation.Clicked += async (_, _) => await EnableAndShowOwnLocationAsync();
        _historyMember = new Picker { Title = "Choose a household member", ItemDisplayBinding = new Binding(nameof(FamilyMemberDto.DisplayName)) };
        _historyMember.SelectedIndexChanged += (_, _) =>
        {
            var selected = _historyMember.SelectedItem is FamilyMemberDto;
            _showHistory.IsEnabled = selected;
            _showTrips.IsEnabled = selected;
        };
        _historyStatus = new Label { Text = "Choose a member to view permitted location history.", FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
        _historyStatus.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _showHistory = new Button { Text = "Show last 24 hours", IsEnabled = false, FontSize = 13 };
        _showHistory.Clicked += async (_, _) => await ShowHistoryAsync();
        _showTrips = new Button { Text = "Show trip summaries", IsEnabled = false, FontSize = 13 };
        _showTrips.Clicked += async (_, _) => await ShowTripsAsync();

        _map = new WebView
        {
            HeightRequest = 480,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var attribution = new Label
        {
            Text = "Map data © OpenStreetMap contributors · Map tiles are loaded from OpenStreetMap.",
            FontSize = 11,
            LineBreakMode = LineBreakMode.WordWrap
        };
        attribution.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var card = new Border
        {
            Padding = 12,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { heading, explanation, _status, _showMyLocation, _refresh, new Label { Text = "Location history and trips", FontSize = 16, FontAttributes = FontAttributes.Bold }, _historyMember, _showHistory, _showTrips, _historyStatus, _map, attribution }
            }
        };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12),
                Spacing = 12,
                Children = { card }
            }
        };

        _map.Source = new HtmlWebViewSource { Html = BuildMapHtml([], []) };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isVisible = true;
        _showMyLocation.Text = _deviceLocation.IsEnabled
            ? "Update my location on this map"
            : "Show my location on this map";
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
        await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        _isVisible = false;
        base.OnDisappearing();
    }

    private async Task EnableAndShowOwnLocationAsync()
    {
        if (!await DisplayAlertAsync(
            "Use your device location?",
            "FamilyOS will ask Android for foreground location permission so it can place your current position on this map. Your position stays on this device; this action does not share it with your household or save location history.",
            "Continue", "Not now"))
            return;

        try
        {
            _showMyLocation.IsEnabled = false;
            if (!await _deviceLocation.EnableAsync())
            {
                _status.Text = "Location permission was not granted. You can still view locations household members have shared with you.";
                return;
            }

            _ownLocation = null;
            _status.Text = "Getting your current device location…";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not access your device location: {ex.Message}";
        }
        finally
        {
            _showMyLocation.IsEnabled = true;
        }
    }

    private async Task LoadFamilyMembersAsync()
    {
        try
        {
            _family = await _api.GetFamilyAsync();
            var visibleIds = _currentLocations.Select(x => x.MemberId).Append(_family.CurrentMemberId).ToHashSet();
            _historyMember.ItemsSource = _family.Members
                .Where(x => x.IsActive && visibleIds.Contains(x.Id))
                .ToList();
            _historyMember.SelectedItem = _family.Members.FirstOrDefault(x => x.Id == _family.CurrentMemberId);
            var selected = _historyMember.SelectedItem is FamilyMemberDto;
            _showHistory.IsEnabled = selected;
            _showTrips.IsEnabled = selected;
        }
        catch (Exception ex)
        {
            _historyStatus.Text = $"Could not load household members: {ex.Message}";
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
            _map.Source = new HtmlWebViewSource { Html = BuildMapHtml(_currentLocations, places, history) };
            _historyStatus.Text = history.Count < 2
                ? $"{member.DisplayName}: not enough saved points to draw a route in the last 24 hours."
                : $"{member.DisplayName}: showing {history.Count} history points from the last 24 hours.";
        }
        catch (Exception ex)
        {
            _historyStatus.Text = $"Location history is unavailable: {ex.Message}";
        }
    }

    private async Task ShowTripsAsync()
    {
        if (_historyMember.SelectedItem is not FamilyMemberDto member) return;
        try
        {
            var trips = await _api.GetLocationTripsAsync(member.Id, DateTimeOffset.UtcNow.AddHours(-24));
            _historyStatus.Text = trips.Count == 0
                ? $"{member.DisplayName}: no trips met the minimum distance and duration thresholds in the last 24 hours."
                : string.Join("\n", trips.Select((trip, index) =>
                    $"Trip {index + 1}: {trip.StartedAtUtc.ToLocalTime():t}–{trip.EndedAtUtc.ToLocalTime():t} · {trip.DistanceMeters / 1000:0.0} km · {trip.DurationMinutes:0} min"));
        }
        catch (Exception ex)
        {
            _historyStatus.Text = $"Trip summaries are unavailable: {ex.Message}";
        }
    }

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        _refresh.IsEnabled = false;
        _status.Text = "Loading locations shared by your household…";
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

            // Show this phone's fix locally without uploading it or granting household access.
            if (_deviceLocation.IsEnabled && (_ownLocation is null ||
                DateTimeOffset.UtcNow - _ownLocationReadAtUtc > TimeSpan.FromMinutes(2)))
            {
                try
                {
                    _ownLocation = await _deviceLocation.GetCurrentLocationAsync();
                    _ownLocationReadAtUtc = DateTimeOffset.UtcNow;
                }
                catch
                {
                    _ownLocation = null;
                }
            }

            if (_ownLocation is not null && _family is not null)
            {
                var ownMember = _family.Members.FirstOrDefault(x => x.Id == _family.CurrentMemberId);
                if (ownMember is not null)
                {
                    valid.RemoveAll(x => x.MemberId == ownMember.Id);
                    valid.Add(new FamilyLocationDto(
                        ownMember.Id, ownMember.DisplayName,
                        _ownLocation.Latitude, _ownLocation.Longitude,
                        _ownLocation.Accuracy, _ownLocation.Timestamp.ToUniversalTime()));
                }
            }

            _currentLocations = valid;

            // Refresh the history picker after shared locations load. On the first
            // page appearance the family list is loaded before the map, so filtering
            // only during LoadFamilyMembersAsync would otherwise hide every other
            // member until the page is reopened.
            if (_family is not null)
            {
                var previouslySelectedId = (_historyMember.SelectedItem as FamilyMemberDto)?.Id;
                var visibleIds = valid.Select(x => x.MemberId)
                    .Append(_family.CurrentMemberId)
                    .ToHashSet();
                var availableMembers = _family.Members
                    .Where(x => x.IsActive && visibleIds.Contains(x.Id))
                    .ToList();
                _historyMember.ItemsSource = availableMembers;
                _historyMember.SelectedItem = availableMembers.FirstOrDefault(x => x.Id == previouslySelectedId)
                    ?? availableMembers.FirstOrDefault(x => x.Id == _family.CurrentMemberId);
                var hasSelection = _historyMember.SelectedItem is FamilyMemberDto;
                _showHistory.IsEnabled = hasSelection;
                _showTrips.IsEnabled = hasSelection;
            }

            _map.Source = new HtmlWebViewSource { Html = BuildMapHtml(valid, places) };
            var ownVisible = _ownLocation is not null && _family is not null;
            _status.Text = valid.Count == 0
                ? "No recent location is available. Enable device location on the Location page to show this phone on the map."
                : ownVisible
                    ? $"Your phone and {Math.Max(0, valid.Count - 1)} shared household location(s) are shown. Your own pin stays on this device unless you separately share it."
                    : $"{valid.Count} shared location(s). Last received update: {valid.Max(x => x.UpdatedAtUtc!.Value).ToLocalTime():g}.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not load household locations: {ex.Message}";
        }
        finally
        {
            _loading = false;
            _refresh.IsEnabled = true;
        }
    }

    private static string BuildMapHtml(IReadOnlyList<FamilyLocationDto> locations, IReadOnlyList<SavedPlaceDto> places, IReadOnlyList<LocationHistoryPointDto>? history = null)
    {
        var data = locations.Select(x => new MapPin(
            x.DisplayName,
            x.Latitude!.Value,
            x.Longitude!.Value,
            x.AccuracyMeters,
            x.UpdatedAtUtc!.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var placeJson = JsonSerializer.Serialize(places, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var historyJson = JsonSerializer.Serialize(history ?? [], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1">
              <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"
                integrity="sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=" crossorigin="">
              <style>
                html, body, #map { height: 100%; width: 100%; margin: 0; padding: 0; }
                body { font-family: sans-serif; }
                .empty { padding: 24px; color: #475569; font-size: 15px; }
                .leaflet-popup-content { line-height: 1.5; }
              </style>
            </head>
            <body>
              <div id="map"></div>
              <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"
                integrity="sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=" crossorigin=""></script>
              <script>
                const locations = {{json}};
                const places = {{placeJson}};
                const history = {{historyJson}};
                const map = L.map('map', { zoomControl: true }).setView([39.5, -98.35], 4);
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                  maxZoom: 19,
                  attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                }).addTo(map);
                if (locations.length === 0 && places.length === 0 && history.length === 0) {
                  document.getElementById('map').innerHTML = '<div class="empty">No shared locations available.</div>';
                } else {
                  const bounds = [];
                  if (history.length > 1) {
                    const routePoints = history.map(point => [point.latitude, point.longitude]);
                    routePoints.forEach(point => bounds.push(point));
                    L.polyline(routePoints, { color: '#dc2626', weight: 4, opacity: 0.8 }).addTo(map);
                  }
                  for (const place of places) {
                    const point = [place.latitude, place.longitude];
                    bounds.push(point);
                    const popup = document.createElement('div');
                    const name = document.createElement('strong');
                    name.textContent = place.name || 'Saved place';
                    const detail = document.createElement('div');
                    detail.textContent = 'Geofence radius: ' + Math.round(place.radiusMeters) + ' m';
                    popup.append(name, detail);
                    L.circle(point, { radius: place.radiusMeters, color: '#2563eb', weight: 2, fillOpacity: 0.08 }).addTo(map).bindPopup(popup);
                  }
                  for (const item of locations) {
                    const point = [item.latitude, item.longitude];
                    bounds.push(point);
                    const popup = document.createElement('div');
                    const name = document.createElement('strong');
                    name.textContent = item.displayName || 'Household member';
                    const updated = document.createElement('div');
                    const ageMinutes = (Date.now() - new Date(item.updatedAtUtc).getTime()) / 60000;
                    const freshness = ageMinutes < 2 ? 'Recently updated' : ageMinutes < 15 ? 'A few minutes old' : ageMinutes < 60 ? 'Stale location' : 'Old location';
                    updated.textContent = freshness + ' · Last shared: ' + new Date(item.updatedAtUtc).toLocaleString();
                    popup.append(name, updated);
                    if (Number.isFinite(item.accuracyMeters)) {
                      const accuracy = document.createElement('div');
                      accuracy.textContent = 'Reported accuracy: ±' + Math.round(item.accuracyMeters) + ' m';
                      popup.append(accuracy);
                      L.circle(point, { radius: Math.min(Math.max(item.accuracyMeters, 5), 10000), weight: 1, fillOpacity: 0.08 }).addTo(map);
                    }
                    L.marker(point).addTo(map).bindPopup(popup);
                  }
                  map.fitBounds(bounds, { padding: [28, 28], maxZoom: 15 });
                }
              </script>
            </body>
            </html>
            """;
    }

    private sealed record MapPin(string DisplayName, double Latitude, double Longitude, double? AccuracyMeters, string UpdatedAtUtc);
}
