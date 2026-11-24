using System.Globalization;
using System.Text.Json;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Full-screen household map. Own device location is requested automatically when the tab opens.
/// Shared household pins only appear when members have explicitly shared.
/// </summary>
public sealed class FamilyMapPage : ContentPage
{
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
    private readonly Button _locate = new() { Text = "⌖", FontSize = 20, WidthRequest = 48, HeightRequest = 48 };
    private readonly Button _refresh = new() { Text = "↻", FontSize = 20, WidthRequest = 48, HeightRequest = 48 };
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
    private bool _loading;
    private bool _isVisible;
    private bool _timerStarted;
    private bool _autoLocated;

    public FamilyMapPage(FamilyApiClient api, DeviceLocationService deviceLocation)
    {
        _api = api;
        _deviceLocation = deviceLocation;
        Title = "Map";

        _status.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        _status.Text = "Finding your location…";
        _locate.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _showHistory.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _showTrips.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");

        _locate.Clicked += async (_, _) => await EnsureOwnLocationAsync(forcePrompt: true);
        _refresh.Clicked += async (_, _) => await RefreshAsync();
        _historyMember.SelectedIndexChanged += (_, _) =>
        {
            var selected = _historyMember.SelectedItem is FamilyMemberDto;
            _showHistory.IsEnabled = selected;
            _showTrips.IsEnabled = selected;
        };
        _showHistory.Clicked += async (_, _) => await ShowHistoryAsync();
        _showTrips.Clicked += async (_, _) => await ShowTripsAsync();

        var statusChip = new Border
        {
            Padding = new Thickness(12, 8),
            StrokeThickness = 0,
            Content = _status,
            HorizontalOptions = LayoutOptions.Fill
        };
        statusChip.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        statusChip.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 };

        var topOverlay = new VerticalStackLayout
        {
            Padding = new Thickness(12, 8, 12, 0),
            Spacing = 8,
            Children = { statusChip }
        };

        var fabColumn = new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 12, 100),
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
            Padding = new Thickness(12, 0, 12, 12),
            Children = { bottomBar }
        };
        root.Add(bottomHost, 0, 0);

        Content = root;
        _map.Source = new HtmlWebViewSource { Html = BuildMapHtml([], []) };
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

    private async Task EnsureOwnLocationAsync(bool forcePrompt)
    {
        try
        {
            _locate.IsEnabled = false;
            if (!_deviceLocation.IsEnabled || forcePrompt)
            {
                if (!await _deviceLocation.EnableAsync())
                {
                    _status.Text = "Location permission off — shared household pins still load.";
                    return;
                }
            }

            _status.Text = "Getting your location…";
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
            _historyViewActive = true;
            _map.Source = new HtmlWebViewSource { Html = BuildMapHtml(_currentLocations, places, _visibleHistory) };
            _historyStatus.IsVisible = true;
            _historyStatus.Text = history.Count < 2
                ? $"{member.DisplayName}: not enough points in the last 24 hours."
                : $"{member.DisplayName}: {history.Count} points · last 24 hours.";
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
                : string.Join(" · ", trips.Select((trip, index) =>
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
                catch { }
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
                        _ownLocation.Accuracy, _ownLocation.Timestamp.UtcDateTime));
                }
            }

            _currentLocations = valid;

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

            _map.Source = new HtmlWebViewSource
            {
                Html = BuildMapHtml(valid, places, _historyViewActive ? _visibleHistory : null)
            };

            var ownVisible = _ownLocation is not null;
            if (valid.Count == 0)
                _status.Text = "No pins yet — enable location or wait for shared updates.";
            else if (ownVisible)
                _status.Text = $"You · {Math.Max(0, valid.Count - 1)} shared · updated {DateTime.Now:t}";
            else
                _status.Text = $"{valid.Count} shared · latest {valid.Max(x => x.UpdatedAtUtc!.Value).ToLocalTime():t}";
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
              <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
              <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"
                integrity="sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=" crossorigin="">
              <style>
                html, body, #map { height: 100%; width: 100%; margin: 0; padding: 0; background: #F4F0E8; }
                body { font-family: -apple-system, system-ui, sans-serif; }
                .empty { display:flex; align-items:center; justify-content:center; height:100%; color:#64748B; font-size:15px; padding:24px; text-align:center; }
                .leaflet-popup-content { line-height: 1.45; margin: 10px 12px; }
                .leaflet-control-attribution { font-size: 10px; }
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
                const map = L.map('map', { zoomControl: true, attributionControl: true }).setView([39.5, -98.35], 4);
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                  maxZoom: 19,
                  attribution: '&copy; OpenStreetMap'
                }).addTo(map);
                if (locations.length === 0 && places.length === 0 && history.length === 0) {
                  document.getElementById('map').innerHTML = '<div class="empty">Waiting for location…</div>';
                } else {
                  const bounds = [];
                  if (history.length > 1) {
                    const routePoints = history.map(point => [point.latitude, point.longitude]);
                    routePoints.forEach(point => bounds.push(point));
                    L.polyline(routePoints, { color: '#0EA5E9', weight: 4, opacity: 0.85 }).addTo(map);
                  }
                  for (const place of places) {
                    const point = [place.latitude, place.longitude];
                    bounds.push(point);
                    const popup = document.createElement('div');
                    const name = document.createElement('strong');
                    name.textContent = place.name || 'Saved place';
                    const detail = document.createElement('div');
                    detail.textContent = 'Geofence · ' + Math.round(place.radiusMeters) + ' m';
                    popup.append(name, detail);
                    L.circle(point, { radius: place.radiusMeters, color: '#0EA5E9', weight: 2, fillOpacity: 0.08 }).addTo(map).bindPopup(popup);
                  }
                  for (const item of locations) {
                    const point = [item.latitude, item.longitude];
                    bounds.push(point);
                    const popup = document.createElement('div');
                    const name = document.createElement('strong');
                    name.textContent = item.displayName || 'Member';
                    const updated = document.createElement('div');
                    const ageMinutes = (Date.now() - new Date(item.updatedAtUtc).getTime()) / 60000;
                    const freshness = ageMinutes < 2 ? 'Live' : ageMinutes < 15 ? 'Recent' : ageMinutes < 60 ? 'Stale' : 'Old';
                    updated.textContent = freshness + ' · ' + new Date(item.updatedAtUtc).toLocaleString();
                    popup.append(name, updated);
                    if (Number.isFinite(item.accuracyMeters)) {
                      L.circle(point, { radius: Math.min(Math.max(item.accuracyMeters, 5), 10000), weight: 1, color: '#0F172A', fillOpacity: 0.06 }).addTo(map);
                    }
                    L.marker(point).addTo(map).bindPopup(popup);
                  }
                  if (bounds.length === 1) {
                    map.setView(bounds[0], 15);
                  } else if (bounds.length > 1) {
                    map.fitBounds(bounds, { padding: [40, 40], maxZoom: 16 });
                  }
                }
                setTimeout(() => map.invalidateSize(), 200);
              </script>
            </body>
            </html>
            """;
    }

    private sealed record MapPin(string DisplayName, double Latitude, double Longitude, double? AccuracyMeters, string UpdatedAtUtc);
}
