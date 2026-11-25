using System.Globalization;
using System.Text.Json;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Full-screen household map with explicit location sharing controls.
/// Sharing is opt-in; coordinates only leave the device when the user shares.
/// </summary>
public sealed class FamilyMapPage : ContentPage, IQueryAttributable
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
        _shareNow.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _placesBtn.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _locationSettings.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _shareLabel.Text = "Sharing: …";

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
                        Children = { _shareNow, _placesBtn, _locationSettings }
                    }
                }
            }
        };
        shareBar.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        shareBar.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 };

        var topOverlay = new VerticalStackLayout
        {
            Padding = new Thickness(12, 8, 12, 0),
            Spacing = 8,
            Children = { statusChip, shareBar }
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
            ? "Sharing on — household can see your pin when you share"
            : "Sharing off — only you see your location on this device";
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

            _status.Text = "Getting your location…";
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
            _status.Text = $"Shared {DateTime.Now:t} · {_ownLocation.Latitude:F5}, {_ownLocation.Longitude:F5}";
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
                catch { /* keep last known */ }
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
                var previouslySelectedId = _requestedMemberId ?? (_historyMember.SelectedItem as FamilyMemberDto)?.Id;
                var availableMembers = _family.Members
                    .Where(x => x.IsActive)
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
                Html = BuildMapHtml(valid, places, _historyViewActive ? _visibleHistory : null, _requestedMemberId ?? (_historyMember.SelectedItem as FamilyMemberDto)?.Id)
            };

            var ownVisible = _ownLocation is not null;
            var selectedMember = _historyMember.SelectedItem as FamilyMemberDto;
            if (selectedMember is not null
                && selectedMember.Id != _family?.CurrentMemberId
                && !valid.Any(x => x.MemberId == selectedMember.Id))
                _status.Text = $"{selectedMember.DisplayName} isn't sharing a location right now.";
            else if (valid.Count == 0)
                _status.Text = "No pins yet — turn on sharing and tap Share now, or wait for household updates.";
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

    private static string BuildMapHtml(IReadOnlyList<FamilyLocationDto> locations, IReadOnlyList<SavedPlaceDto> places, IReadOnlyList<LocationHistoryPointDto>? history = null, Guid? selectedMemberId = null)
    {
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
                const selectedMemberId = {{JsonSerializer.Serialize(selectedMemberId?.ToString())}};
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
                    const marker = L.marker(point).addTo(map).bindPopup(popup);
                    if (selectedMemberId && item.memberId === selectedMemberId) marker.openPopup();
                  }
                  if (bounds.length) map.fitBounds(bounds, { padding: [40, 40], maxZoom: 16 });
                }
                setTimeout(() => map.invalidateSize(), 120);
              </script>
            </body>
            </html>
            """;
    }

    private sealed record MapPin(Guid MemberId, string DisplayName, double Latitude, double Longitude, double? AccuracyMeters, string UpdatedAtUtc);
}
