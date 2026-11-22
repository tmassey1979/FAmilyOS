using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Secondary navigation hub (issue #43): household tools, activity, account, and developer routes.
/// </summary>
public sealed class MorePage : ContentPage
{
    private readonly NotificationsApiClient _notifications;
    private readonly IThemeService _theme;
    private readonly IAuthSessionService _auth;
    private readonly VerticalStackLayout _root;
    private readonly Label _status;
    private Label? _unreadBadge;
    private Label? _themeLabel;
    private bool _loading;

    public MorePage(
        NotificationsApiClient notifications,
        IThemeService theme,
        IAuthSessionService auth)
    {
        _notifications = notifications;
        _theme = theme;
        _auth = auth;
        Title = "More";

        _status = new Label { FontSize = 13 };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var heading = new Label { Text = "More", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");

        _root = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12),
            Spacing = 16,
            Children =
            {
                heading,
                _status,
                Section("Household", BuildHouseholdRows()),
                Section("Activity", BuildActivityRows()),
                Section("Account", BuildAccountRows()),
                Section("Developer", BuildDeveloperRows())
            }
        };

        Content = new RefreshView
        {
            Content = new ScrollView { Content = _root },
            Command = new Command(async () => await RefreshUnreadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshUnreadAsync();
    }

    private async Task RefreshUnreadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var rows = await _notifications.GetMineAsync();
            var unread = rows.Count(x => !x.IsRead);
            if (_unreadBadge is not null)
            {
                _unreadBadge.Text = unread > 0 ? $"{unread} unread" : "All caught up";
                _unreadBadge.IsVisible = true;
            }
            _status.Text = unread > 0
                ? $"{unread} unread notification{(unread == 1 ? "" : "s")}"
                : "Household tools and account settings";
        }
        catch (FamilyApiException)
        {
            if (_unreadBadge is not null)
            {
                _unreadBadge.Text = "Sign in to sync";
                _unreadBadge.IsVisible = true;
            }
            _status.Text = "Sign in to load live notification counts.";
        }
        catch
        {
            _status.Text = "Household tools and account settings";
        }
        finally
        {
            _loading = false;
        }
    }

    private IEnumerable<View> BuildHouseholdRows()
    {
        yield return NavRow("Requests", "Approvals, drafts, and money asks", "menu_requests.png", nameof(RequestsPage));
        yield return NavRow("Meals & recipes", "Weekly plan and shopping hand-off", "menu_meals.png", nameof(MealsPage));
        yield return NavRow("Family chat", "Household conversations", "menu_chat.png", nameof(ChatPage));
    }

    private IEnumerable<View> BuildActivityRows()
    {
        var notif = NavRow("Notifications", "Inbox and deep links", "menu_notifications.png", nameof(NotificationsPage));
        // Attach badge label into the meta area by wrapping — store reference from helper.
        yield return notif;
        yield return NavRow("Outcomes", "Approval rates and household progress", "menu_outcomes.png", nameof(OutcomesPage));
    }

    private IEnumerable<View> BuildAccountRows()
    {
        yield return NavRow("Settings", "Quiet hours, Google Calendar, account", "menu_settings.png", nameof(SettingsPage));
        yield return ThemeRow();
        yield return ActionRow("Sign out", "Clear this device session", async () => await SignOutAsync());
    }

    private IEnumerable<View> BuildDeveloperRows()
    {
        yield return NavRow("Design system", "Fo* controls and tokens gallery", "menu_gallery.png", nameof(DesignSystemGalleryPage));
        yield return NavRow("Startup diagnostics", "Launch timeline and logs", "menu_settings.png", nameof(StartupDiagnosticsPage));
        yield return NavRow("Access token", "Inspect the current OIDC token", "menu_settings.png", nameof(AccessTokenPage));
    }

    private static View Section(string title, IEnumerable<View> rows)
    {
        var heading = new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        var stack = new VerticalStackLayout { Spacing = 8, Children = { heading } };
        foreach (var row in rows)
            stack.Children.Add(row);
        return stack;
    }

    private View NavRow(string title, string subtitle, string icon, string route)
    {
        var titleLabel = new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        titleLabel.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var subLabel = new Label { Text = subtitle, FontSize = 12, LineBreakMode = LineBreakMode.TailTruncation };
        subLabel.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var textCol = new VerticalStackLayout { Spacing = 2, Children = { titleLabel, subLabel }, VerticalOptions = LayoutOptions.Center };

        // Notifications get a live unread badge.
        View trailing;
        if (route == nameof(NotificationsPage))
        {
            _unreadBadge = new Label { Text = "…", FontSize = 12, VerticalOptions = LayoutOptions.Center };
            _unreadBadge.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            trailing = _unreadBadge;
        }
        else
        {
            trailing = new Label
            {
                Text = "›",
                FontSize = 20,
                VerticalOptions = LayoutOptions.Center
            };
            trailing.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        }

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14,
            Padding = new Thickness(2, 0)
        };
        grid.Add(new Image
        {
            Source = icon,
            WidthRequest = 24,
            HeightRequest = 24,
            VerticalOptions = LayoutOptions.Center
        }, 0);
        grid.Add(textCol, 1);
        grid.Add(trailing, 2);

        var border = new Border { Padding = new Thickness(14, 12), Content = grid };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            try { await Shell.Current.GoToAsync(route); }
            catch { _status.Text = $"Could not open {title}."; }
        };
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private View ThemeRow()
    {
        _themeLabel = new Label
        {
            Text = ThemeCaption(_theme.Preference),
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        };
        _themeLabel.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var titleLabel = new Label { Text = "Appearance", FontSize = 16, FontAttributes = FontAttributes.Bold };
        titleLabel.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var subLabel = new Label { Text = "Tap to cycle System → Light → Dark", FontSize = 12 };
        subLabel.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14
        };
        grid.Add(new Image
        {
            Source = "menu_settings.png",
            WidthRequest = 24,
            HeightRequest = 24,
            VerticalOptions = LayoutOptions.Center
        }, 0);
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { titleLabel, subLabel } }, 1);
        grid.Add(_themeLabel, 2);

        var border = new Border { Padding = new Thickness(14, 12), Content = grid };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            var next = _theme.Preference switch
            {
                AppThemePreference.System => AppThemePreference.Light,
                AppThemePreference.Light => AppThemePreference.Dark,
                _ => AppThemePreference.System
            };
            _theme.SetPreference(next);
            if (_themeLabel is not null)
                _themeLabel.Text = ThemeCaption(next);
            _status.Text = $"Theme set to {next}.";
        };
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private View ActionRow(string title, string subtitle, Func<Task> action)
    {
        var titleLabel = new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        titleLabel.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var subLabel = new Label { Text = subtitle, FontSize = 12 };
        subLabel.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 14
        };
        grid.Add(new Image
        {
            Source = "menu_settings.png",
            WidthRequest = 24,
            HeightRequest = 24,
            VerticalOptions = LayoutOptions.Center
        }, 0);
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { titleLabel, subLabel } }, 1);

        var border = new Border { Padding = new Thickness(14, 12), Content = grid };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await action();
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private async Task SignOutAsync()
    {
        var ok = await DisplayAlertAsync("Sign out", "Clear the saved session on this device?", "Sign out", "Cancel");
        if (!ok) return;
        try
        {
            await _auth.SignOutAsync();
            _status.Text = "Signed out on this device.";
            if (_unreadBadge is not null)
                _unreadBadge.Text = "Sign in to sync";
            await Shell.Current.GoToAsync(nameof(OnboardingPage));
        }
        catch
        {
            _status.Text = "Could not clear the session. Try again.";
        }
    }

    private static string ThemeCaption(AppThemePreference preference) => preference switch
    {
        AppThemePreference.Light => "Light",
        AppThemePreference.Dark => "Dark",
        _ => "System"
    };
}
