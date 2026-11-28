using FamilyOS.Maui.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Merge-2 target layout: crest header, presence row, STATUS panel with
/// L-brackets, module grid. Greeting uses the device local clock.
/// </summary>
public sealed class HomePage : ContentPage
{
    private readonly PulseApiClient _pulseApi;
    private readonly FamilyApiClient _familyApi;
    private readonly KeycloakAuthService _auth;
    private readonly PulseRefreshSignal _pulseRefresh;

    private readonly Label _greeting = new()
    {
        FontSize = 26,
        FontAttributes = FontAttributes.Bold,
        LineBreakMode = LineBreakMode.WordWrap
    };
    private readonly Label _roleHint = new() { FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
    private readonly VerticalStackLayout _nextUpHost = new() { Spacing = 8 };
    private readonly Grid _statsGrid = new()
    {
        ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star)
        },
        ColumnSpacing = 8
    };
    private readonly Grid _moduleGrid = new()
    {
        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
        RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
        ColumnSpacing = 10,
        RowSpacing = 10
    };
    private readonly HorizontalStackLayout _members = new() { Spacing = 12 };
    private readonly Button _editToggle = new() { Text = "Edit", FontSize = 13, Padding = new Thickness(12, 6) };
    private bool _editMode;
    private bool _loading;
    private FamilyDto? _family;

    public HomePage(
        PulseApiClient pulseApi,
        FamilyApiClient familyApi,
        KeycloakAuthService auth,
        PulseRefreshSignal pulseRefresh)
    {
        _pulseApi = pulseApi;
        _familyApi = familyApi;
        _auth = auth;
        _pulseRefresh = pulseRefresh;
        _pulseRefresh.RefreshRequested += OnPulseRefreshRequested;

        Title = "Home";
        _greeting.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        _roleHint.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        _editToggle.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _editToggle.Clicked += (_, _) => ToggleEditMode();

        Content = new RefreshView
        {
            Content = new ScrollView { Content = BuildShell() },
            Command = new Command(async () => await LoadAsync())
        };
    }

    private View BuildShell()
    {
        var crest = new Label
        {
            Text = "\u25C8",
            FontSize = 15,
            VerticalTextAlignment = TextAlignment.Center
        };
        crest.SetDynamicResource(Label.TextColorProperty, "AccentPrimary");

        var brand = new Label
        {
            Text = "FAMILY OS",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 2.2,
            VerticalTextAlignment = TextAlignment.Center
        };
        brand.SetDynamicResource(Label.TextColorProperty, "AccentGlow");

        var brandRow = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children = { crest, brand }
        };

        var top = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        top.Add(brandRow, 0, 0);
        top.Add(_editToggle, 1, 0);

        var scan = new BoxView { HeightRequest = 2, Opacity = 0.9, Margin = new Thickness(0, 2, 0, 0) };
        scan.SetDynamicResource(BoxView.ColorProperty, "AccentPrimary");

        var membersScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            HeightRequest = 76,
            Content = _members
        };

        return new VerticalStackLayout
        {
            Padding = new Thickness(16, 10, 16, 28),
            Spacing = 14,
            Children =
            {
                top,
                scan,
                membersScroll,
                BuildStatusPanel(),
                _statsGrid,
                SectionLabel("COMMAND MODULES"),
                _moduleGrid
            }
        };
    }

    private View BuildStatusPanel()
    {
        var eyebrow = new Label
        {
            Text = "STATUS",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.6
        };
        eyebrow.SetDynamicResource(Label.TextColorProperty, "AccentGlow");

        var body = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { eyebrow, _greeting, _roleHint, _nextUpHost }
        };

        var rail = new BoxView { WidthRequest = 4 };
        rail.SetDynamicResource(BoxView.ColorProperty, "AccentPrimary");

        var inner = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12
        };
        inner.Add(rail, 0, 0);
        inner.Add(body, 1, 0);

        var border = new Border
        {
            Padding = new Thickness(14, 14),
            StrokeThickness = 1,
            Content = inner
        };
        border.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        border.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        border.StrokeShape = new RoundRectangle { CornerRadius = 16 };
        return border;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.4,
            Margin = new Thickness(2, 4, 0, 0)
        };
        label.SetDynamicResource(Label.TextColorProperty, "AccentGlow");
        return label;
    }

    private void OnPulseRefreshRequested(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loading)
            await LoadAsync();
    }

    private void ToggleEditMode()
    {
        _editMode = !_editMode;
        _editToggle.Text = _editMode ? "Done" : "Edit";
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            if (!_auth.IsAuthenticated)
            {
                ShowSignedOut();
                return;
            }

            var pulse = await _pulseApi.GetPulseAsync();
            try { _family = await _familyApi.GetMyFamilyAsync(); }
            catch { _family = null; }

            BindMembers(_family);
            _greeting.Text = LocalGreeting(pulse.Greeting);
            _roleHint.Text = pulse.RoleHint ?? "Your household at a glance.";
            BindNextUp(pulse);
            BindStats(pulse);
            BindModules(pulse);
        }
        catch (Exception ex)
        {
            _greeting.Text = "Could not load pulse";
            _roleHint.Text = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private void ShowSignedOut()
    {
        _greeting.Text = "Sign in to open Family OS";
        _roleHint.Text = "Your household command center.";
        _nextUpHost.Children.Clear();
        _statsGrid.Children.Clear();
        _moduleGrid.Children.Clear();
        _members.Children.Clear();
    }

    private void BindMembers(FamilyDto? family)
    {
        _members.Children.Clear();
        if (family is null) return;
        foreach (var member in family.Members.Where(m => m.IsActive).OrderBy(m => m.DisplayName))
            _members.Children.Add(BuildMemberChip(member, family.CurrentMemberId == member.Id));
    }

    private View BuildMemberChip(FamilyMemberDto member, bool isSelf)
    {
        var initials = string.Join("", member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p[0]).Take(2)).ToUpperInvariant();
        if (string.IsNullOrEmpty(initials)) initials = "?";

        var avatar = new Border
        {
            WidthRequest = 40,
            HeightRequest = 40,
            StrokeThickness = isSelf ? 2 : 1,
            Content = new Label
            {
                Text = initials,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        avatar.SetDynamicResource(Border.BackgroundColorProperty, "BgElevated");
        avatar.SetDynamicResource(Border.StrokeProperty, isSelf ? "AccentPrimary" : "BorderSubtle");
        avatar.StrokeShape = new RoundRectangle { CornerRadius = 20 };

        var name = new Label
        {
            Text = isSelf ? "You" : FirstName(member.DisplayName),
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            WidthRequest = 56
        };
        name.SetDynamicResource(Label.TextColorProperty, "TextSecondary");

        return new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Center,
            Children = { avatar, name }
        };
    }

    private void BindNextUp(PulseDto pulse)
    {
        _nextUpHost.Children.Clear();
        if (pulse.NextAction is null) return;

        var title = new Label
        {
            Text = pulse.NextAction.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

        var sub = new Label
        {
            Text = pulse.NextAction.Subtitle ?? pulse.NextAction.DueLabel ?? pulse.NextAction.Kind,
            FontSize = 12
        };
        sub.SetDynamicResource(Label.TextColorProperty, "TextSecondary");

        var cta = new Button
        {
            Text = pulse.NextAction.PrimaryAction,
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(14, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        cta.BackgroundColor = Color.FromArgb("#22D3EE");
        cta.TextColor = Color.FromArgb("#0A0E14");
        cta.CornerRadius = 18;
        var action = pulse.NextAction;
        cta.Clicked += async (_, _) => await OpenNextActionAsync(action);

        _nextUpHost.Children.Add(new Label
        {
            Text = "NEXT UP",
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.2,
            TextColor = Color.FromArgb("#22D3EE")
        });
        _nextUpHost.Children.Add(title);
        _nextUpHost.Children.Add(sub);
        _nextUpHost.Children.Add(cta);
    }

    private void BindStats(PulseDto pulse)
    {
        _statsGrid.Children.Clear();
        var h = pulse.Household;
        AddStat(0, "Approvals", h.RequestsAwaitingApproval.ToString());
        AddStat(1, "Due today", h.TasksDueToday.ToString());
        AddStat(2, "Accept", h.TasksNeedingAcceptance.ToString());
        AddStat(3, "Up next", pulse.ComingUp.Count.ToString());
    }

    private void AddStat(int col, string label, string value)
    {
        var v = new Label { Text = value, FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
        v.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var l = new Label { Text = label, FontSize = 10, HorizontalTextAlignment = TextAlignment.Center };
        l.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var box = new Border
        {
            Padding = new Thickness(6, 10),
            StrokeThickness = 1,
            Content = new VerticalStackLayout { Spacing = 2, Children = { v, l } }
        };
        box.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        box.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        box.StrokeShape = new RoundRectangle { CornerRadius = 12 };
        _statsGrid.Add(box, col, 0);
    }

    private void BindModules(PulseDto pulse)
    {
        _moduleGrid.Children.Clear();
        AddModule(0, 0, "Tasks", "Assigned work", "//tasks");
        AddModule(1, 0, "Requests", "Approvals", "//requests");
        AddModule(0, 1, "Calendar", "Coming up", "//calendar");
        AddModule(1, 1, "Family", "Household", "//family");
        AddModule(0, 2, "Meals", "Plan & shop", "//meals");
        AddModule(1, 2, "Map", "Locations", "//map");
    }

    private void AddModule(int col, int row, string title, string subtitle, string route)
    {
        var t = new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold };
        t.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var s = new Label { Text = subtitle, FontSize = 11 };
        s.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var body = new VerticalStackLayout { Spacing = 4, Children = { t, s }, Padding = new Thickness(14) };
        var border = new Border
        {
            StrokeThickness = 1,
            Content = body
        };
        border.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        border.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        border.StrokeShape = new RoundRectangle { CornerRadius = 14 };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => { try { await Shell.Current.GoToAsync(route); } catch { } };
        border.GestureRecognizers.Add(tap);
        _moduleGrid.Add(border, col, row);
    }

    private async Task OpenNextActionAsync(NextActionDto action)
    {
        try
        {
            var route = action.Kind.ToLowerInvariant() switch
            {
                "task" => "//tasks",
                "request" => "//requests",
                _ => "//home"
            };
            await Shell.Current.GoToAsync(route);
        }
        catch { /* ignore */ }
    }

    private static string FirstName(string displayName)
    {
        var part = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(part) ? displayName : part;
    }

    private static string LocalGreeting(string? pulseGreeting)
    {
        var name = "there";
        if (!string.IsNullOrWhiteSpace(pulseGreeting))
        {
            var parts = pulseGreeting.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                name = parts[1];
            else
            {
                var tokens = pulseGreeting.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0)
                    name = tokens[^1].TrimEnd('.');
            }
        }

        var hour = DateTime.Now.Hour;
        var part = hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };
        return $"{part}, {name}";
    }
}
