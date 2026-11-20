using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Account overview and quiet-hours controls.</summary>
public sealed class SettingsPage : ContentPage
{
    private readonly MeApiClient _me;
    private readonly FamilyApiClient _family;
    private readonly Label _account = new() { FontSize = 14 };
    private readonly Label _household = new() { FontSize = 14 };
    private readonly Label _status = new() { FontSize = 12 };
    private readonly Label _googleStatus = new() { FontSize = 13 };
    private readonly Button _googleConnect = new() { Text = "Connect Google Calendar" };
    private readonly Button _googleSync = new() { Text = "Sync now" };
    private readonly Button _googleDisconnect = new() { Text = "Disconnect" };
    private readonly Switch _enabled = new();
    private readonly Entry _start = new() { Placeholder = "22:00", Keyboard = Keyboard.Numeric, MaxLength = 5, WidthRequest = 100 };
    private readonly Entry _end = new() { Placeholder = "07:00", Keyboard = Keyboard.Numeric, MaxLength = 5, WidthRequest = 100 };
    private readonly Button _save = new() { Text = "Save quiet hours" };
    private readonly Button _refresh = new() { Text = "Refresh" };
    private bool _busy;

    public SettingsPage(MeApiClient me, FamilyApiClient family)
    {
        _me = me;
        _family = family;
        Title = "Settings";
        _save.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _account.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _household.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _save.Clicked += async (_, _) => await SaveAsync();
        _refresh.Clicked += async (_, _) => await LoadAsync();
        _googleConnect.Clicked += async (_, _) => await ConnectGoogleCalendarAsync();
        _googleSync.Clicked += async (_, _) => await SyncGoogleCalendarAsync();
        _googleDisconnect.Clicked += async (_, _) => await DisconnectGoogleCalendarAsync();

        var heading = new Label { Text = "Settings", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var accountCard = Card("Account", _account);
        var familyCard = Card("Household", _household);
        var quietHeading = new Label { Text = "Quiet hours", FontSize = 18, FontAttributes = FontAttributes.Bold };
        quietHeading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var toggleRow = new HorizontalStackLayout
        {
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "Enable quiet hours", VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.StartAndExpand },
                _enabled
            }
        };
        var timeRow = new HorizontalStackLayout
        {
            Spacing = 18,
            Children =
            {
                new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "Start" }, _start } },
                new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "End" }, _end } }
            }
        };
        var quietCard = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    quietHeading,
                    new Label { Text = "Mute non-urgent household notifications during this local-time window.", FontSize = 13 },
                    toggleRow,
                    timeRow,
                    _save
                }
            }
        };
        quietCard.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var googleHeading = new Label { Text = "Google Calendar", FontSize = 18, FontAttributes = FontAttributes.Bold };
        googleHeading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var googleCard = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    googleHeading,
                    new Label { Text = "Connect your personal Google Calendar to sync events with the household calendar. Your FamilyOS calendar remains available without connecting Google.", FontSize = 13 },
                    _googleStatus,
                    new HorizontalStackLayout { Spacing = 8, Children = { _googleConnect, _googleSync, _googleDisconnect } }
                }
            }
        };
        googleCard.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var toolbar = new HorizontalStackLayout { Spacing = 8, Children = { _refresh } };
        Content = new RefreshView
        {
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(18, 12),
                    Spacing = 14,
                    Children = { heading, toolbar, _status, accountCard, familyCard, quietCard, googleCard }
                }
            },
            Command = new Command(async () => await LoadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (string.IsNullOrEmpty(_account.Text)) await LoadAsync();
    }

    private static Border Card(string title, View content)
    {
        var heading = new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var card = new Border { Padding = 16, Content = new VerticalStackLayout { Spacing = 10, Children = { heading, content } } };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return card;
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        _busy = true;
        _refresh.IsEnabled = false;
        _status.Text = "Loading settings…";
        try
        {
            var meTask = _me.GetAsync();
            var familyTask = _family.GetFamilyAsync();
            var quietTask = _me.GetQuietHoursAsync();
            await Task.WhenAll(meTask, familyTask, quietTask);
            var me = await meTask;
            var family = await familyTask;
            var quiet = await quietTask;
            var google = await googleTask;
            _googleStatus.Text = !google.Configured
                ? "Google Calendar integration is not configured on the server."
                : google.Connected
                    ? $"Connected{(string.IsNullOrWhiteSpace(google.Email) ? "" : $" as {google.Email}")}. Last sync: {(google.LastSyncAtUtc.HasValue ? google.LastSyncAtUtc.Value.ToLocalTime().ToString("g") : "not yet synced")}."
                    : "Not connected."; 
            _googleConnect.IsVisible = google.Configured && !google.Connected;
            _googleSync.IsVisible = google.Configured && google.Connected;
            _googleDisconnect.IsVisible = google.Configured && google.Connected;
            _account.Text = $"Role: {me.Role ?? "Member"}\nMember ID: {me.MemberId?.ToString("D") ?? "Unavailable"}";
            _household.Text = $"{family.Name}\n{family.Members.Count(x => x.IsActive)} active members";
            _enabled.IsToggled = quiet.Enabled;
            _start.Text = quiet.StartLabel ?? FormatTime(quiet.StartMinutes);
            _end.Text = quiet.EndLabel ?? FormatTime(quiet.EndMinutes);
            _status.Text = "Settings loaded.";
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (HttpRequestException) { _status.Text = "FamilyOS could not be reached. Check your connection."; }
        catch (Exception) { _status.Text = "Could not load settings. Try refreshing."; }
        finally { _busy = false; _refresh.IsEnabled = true; }
    }

    private async Task ConnectGoogleCalendarAsync()
    {
        try
        {
            var result = await _me.GetGoogleCalendarAuthUrlAsync();
            await Launcher.OpenAsync(result.Url);
            _status.Text = "Finish authorization in your browser, then return here and tap Refresh.";
        }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Could not open Google authorization."; }
    }

    private async Task SyncGoogleCalendarAsync()
    {
        if (_busy) return;
        _busy = true;
        _googleSync.IsEnabled = false;
        try
        {
            var result = await _me.SyncGoogleCalendarAsync();
            _status.Text = $"Google Calendar sync complete. {result.Changed} event(s) updated.";
            _busy = false;
            await LoadAsync();
        }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Google Calendar sync failed."; }
        finally { _busy = false; _googleSync.IsEnabled = true; }
    }

    private async Task DisconnectGoogleCalendarAsync()
    {
        if (_busy) return;
        var confirm = await DisplayAlert("Disconnect Google Calendar", "Stop syncing this Google account with FamilyOS?", "Disconnect", "Cancel");
        if (!confirm) return;
        _busy = true;
        _googleDisconnect.IsEnabled = false;
        try
        {
            await _me.DisconnectGoogleCalendarAsync();
            _status.Text = "Google Calendar disconnected.";
            _busy = false;
            await LoadAsync();
        }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Could not disconnect Google Calendar."; }
        finally { _busy = false; _googleDisconnect.IsEnabled = true; }
    }

    private async Task SaveAsync()
    {
        if (_busy) return;
        var start = ParseTime(_start.Text);
        var end = ParseTime(_end.Text);
        if (_enabled.IsToggled && (start is null || end is null))
        {
            _status.Text = "Enter valid 24-hour times in HH:MM format.";
            return;
        }
        if (_enabled.IsToggled && start == end)
        {
            _status.Text = "Start and end times must be different.";
            return;
        }
        _busy = true;
        _save.IsEnabled = false;
        try
        {
            var saved = await _me.SetQuietHoursAsync(_enabled.IsToggled, start, end);
            _enabled.IsToggled = saved.Enabled;
            _start.Text = saved.StartLabel ?? FormatTime(saved.StartMinutes);
            _end.Text = saved.EndLabel ?? FormatTime(saved.EndMinutes);
            _status.Text = "Quiet hours saved.";
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not save quiet hours. Try again."; }
        finally { _busy = false; _save.IsEnabled = true; }
    }

    private static int? ParseTime(string? value)
    {
        if (!TimeSpan.TryParseExact(value?.Trim(), new[] { @"h\:mm", @"hh\:mm" }, null, out var time)
            || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return null;
        return (int)time.TotalMinutes;
    }

    private static string FormatTime(int? minutes)
        => minutes is >= 0 and < 1440 ? $"{minutes / 60:00}:{minutes % 60:00}" : "22:00";
}
