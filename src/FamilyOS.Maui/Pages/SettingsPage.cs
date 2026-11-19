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
        var toolbar = new HorizontalStackLayout { Spacing = 8, Children = { _refresh } };
        Content = new RefreshView
        {
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(18, 12),
                    Spacing = 14,
                    Children = { heading, toolbar, _status, accountCard, familyCard, quietCard }
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
