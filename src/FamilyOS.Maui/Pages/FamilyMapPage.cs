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
    private readonly WebView _map;
    private readonly Label _status;
    private readonly Button _refresh;
    private bool _loading;

    public FamilyMapPage(FamilyApiClient api)
    {
        _api = api;
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
            Text = "Pins appear only when a household member has explicitly shared a location. A pin is the last shared reading, not a live position.",
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
                Children = { heading, explanation, _status, _refresh, _map, attribution }
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

        _map.Source = new HtmlWebViewSource { Html = BuildMapHtml([]) };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
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
            var valid = locations
                .Where(x => x.Latitude is >= -90 and <= 90
                    && x.Longitude is >= -180 and <= 180
                    && x.UpdatedAtUtc.HasValue)
                .ToList();

            _map.Source = new HtmlWebViewSource { Html = BuildMapHtml(valid) };
            _status.Text = valid.Count == 0
                ? "No active household members have shared a location in the last 24 hours."
                : $"{valid.Count} shared location(s). Pins show the last submitted reading; refresh to check for updates.";
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

    private static string BuildMapHtml(IReadOnlyList<FamilyLocationDto> locations)
    {
        var data = locations.Select(x => new MapPin(
            x.DisplayName,
            x.Latitude!.Value,
            x.Longitude!.Value,
            x.AccuracyMeters,
            x.UpdatedAtUtc!.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
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
                const map = L.map('map', { zoomControl: true }).setView([39.5, -98.35], 4);
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                  maxZoom: 19,
                  attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                }).addTo(map);
                if (locations.length === 0) {
                  document.getElementById('map').innerHTML = '<div class="empty">No shared locations available.</div>';
                } else {
                  const bounds = [];
                  for (const item of locations) {
                    const point = [item.latitude, item.longitude];
                    bounds.push(point);
                    const popup = document.createElement('div');
                    const name = document.createElement('strong');
                    name.textContent = item.displayName || 'Household member';
                    const updated = document.createElement('div');
                    updated.textContent = 'Last shared: ' + new Date(item.updatedAtUtc).toLocaleString();
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
