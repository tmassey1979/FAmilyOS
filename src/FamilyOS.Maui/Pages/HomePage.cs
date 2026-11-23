using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Household home shell: member strip, Pulse command center, and rearrangeable module grid.
/// </summary>
public sealed class HomePage : ContentPage
{
    private readonly PulseApiClient _pulse;
    private readonly FamilyApiClient _family;
    private readonly HorizontalStackLayout _members = new() { Spacing = 14 };
    private readonly Label _greeting = new() { FontSize = 28, FontAttributes = FontAttributes.Bold };
    private readonly Label _status = new() { FontSize = 14 };
    private readonly VerticalStackLayout _pulseSection = new() { Spacing = 10 };
    private readonly FlexLayout _moduleGrid = new()
    {
        Wrap = FlexWrap.Wrap,
        JustifyContent = FlexJustify.Start,
        AlignItems = FlexAlignItems.Start
    };
    private readonly Button _editToggle = new() { Text = "Edit" };
    private List<HomeModule> _modules = [];
    private bool _editMode;
    private bool _loading;
    private Guid? _selectedMemberId;

    public HomePage(PulseApiClient pulse, FamilyApiClient family)
    {
        _pulse = pulse;
        _family = family;
        Title = "Home";

        _greeting.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        _status.SetDynamicResource(Label.TextColorProperty, "TextMuted");
        _editToggle.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _editToggle.Clicked += (_, _) => ToggleEditMode();

        var membersScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _members
        };

        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        var brand = new Label { Text = "FamilyOS", FontSize = 13, FontAttributes = FontAttributes.Bold };
        brand.SetDynamicResource(Label.TextColorProperty, "TextMuted");
        topRow.Add(brand, 0, 0);
        topRow.Add(_editToggle, 1, 0);

        var page = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12, 20, 28),
            Spacing = 14,
            Children =
            {
                topRow,
                membersScroll,
                _greeting,
                _status,
                _pulseSection,
                SectionLabel("Modules"),
                _moduleGrid
            }
        };

        Content = new RefreshView
        {
            Content = new ScrollView { Content = page },
            Command = new Command(async () => await LoadAsync())
        };

        _modules = HomeModuleCatalog.GetOrdered();
        RebuildModuleGrid();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loading)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            await Task.WhenAll(LoadMembersAsync(), LoadPulseAsync());
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadMembersAsync()
    {
        try
        {
            var family = await _family.GetFamilyAsync();
            _members.Children.Clear();
            foreach (var member in family.Members.Where(m => m.IsActive).OrderBy(m => m.DisplayName))
                _members.Children.Add(BuildMemberChip(member, member.Id == family.CurrentMemberId));
        }
        catch
        {
            // Member strip is best-effort; Pulse still loads.
        }
    }

    private View BuildMemberChip(FamilyMemberDto member, bool isSelf)
    {
        var initials = string.Join("", member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(p => char.ToUpperInvariant(p[0])));
        if (string.IsNullOrEmpty(initials)) initials = "?";

        var avatar = new Border
        {
            WidthRequest = 52,
            HeightRequest = 52,
            StrokeThickness = _selectedMemberId == member.Id ? 3 : 0,
            BackgroundColor = Color.FromArgb("#E0F2FE"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 26 },
            Content = new Label
            {
                Text = initials,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#0F172A")
            }
        };
        avatar.SetDynamicResource(Border.StrokeProperty, "AccentSky");

        var name = new Label
        {
            Text = isSelf ? "You" : FirstName(member.DisplayName),
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center
        };
        name.SetDynamicResource(Label.TextColorProperty, "TextMuted");

        var stack = new VerticalStackLayout { Spacing = 4, WidthRequest = 64, Children = { avatar, name } };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            _selectedMemberId = member.Id;
            await LoadMembersAsync();
            _status.Text = $"Focused on {member.DisplayName}";
            // Map focus: jump to Map tab for household locations.
            if (member.Id != default)
                await Shell.Current.GoToAsync("//map");
        };
        stack.GestureRecognizers.Add(tap);
        return stack;
    }

    private async Task LoadPulseAsync()
    {
        _status.Text = "Loading household…";
        _pulseSection.Children.Clear();
        try
        {
            var pulse = await _pulse.GetPulseAsync();
            _greeting.Text = string.IsNullOrWhiteSpace(pulse.Greeting) ? "Welcome home" : pulse.Greeting;
            _status.Text = pulse.RoleHint ?? "Your household at a glance.";

            if (pulse.NextAction is not null)
            {
                var next = pulse.NextAction;
                var btn = new Button
                {
                    Text = string.IsNullOrWhiteSpace(next.PrimaryAction)
                        ? next.Title
                        : $"{next.PrimaryAction}: {next.Title}"
                };
                btn.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
                btn.Clicked += async (_, _) => await OpenKindAsync(next.Kind);
                _pulseSection.Children.Add(PulseCard("Next up", next.Subtitle ?? next.Title, next.DueLabel, btn));
            }
            else
            {
                _pulseSection.Children.Add(PulseCard("Pulse", "You’re caught up — nothing urgent right now.", null, null));
            }

            // Compact stats row
            var stats = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
            stats.Add(MiniStat(pulse.Household.TasksDueToday, "Due"), 0, 0);
            stats.Add(MiniStat(pulse.Household.RequestsAwaitingApproval, "Approvals"), 1, 0);
            stats.Add(MiniStat(pulse.Household.TasksNeedingAcceptance, "Accept"), 2, 0);
            _pulseSection.Children.Add(stats);
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                var signIn = new Button { Text = "Sign in / set up household" };
                signIn.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
                signIn.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(OnboardingPage));
                _pulseSection.Children.Add(signIn);
            }
        }
        catch
        {
            _status.Text = "Could not load Pulse. Pull to refresh.";
        }
    }

    private void ToggleEditMode()
    {
        _editMode = !_editMode;
        _editToggle.Text = _editMode ? "Done" : "Edit";
        RebuildModuleGrid();
    }

    private void RebuildModuleGrid()
    {
        _moduleGrid.Children.Clear();
        for (var i = 0; i < _modules.Count; i++)
        {
            var index = i;
            var module = _modules[i];
            _moduleGrid.Children.Add(BuildModuleTile(module, index));
        }
    }

    private View BuildModuleTile(HomeModule module, int index)
    {
        var icon = new Image
        {
            Source = module.Icon,
            WidthRequest = 28,
            HeightRequest = 28,
            HorizontalOptions = LayoutOptions.Center
        };
        var title = new Label
        {
            Text = module.Title,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

        var body = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(10, 14),
            Children = { icon, title }
        };

        if (_editMode)
        {
            var left = new Button { Text = "←", WidthRequest = 40, FontSize = 14 };
            var right = new Button { Text = "→", WidthRequest = 40, FontSize = 14 };
            left.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            right.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            left.Clicked += (_, _) =>
            {
                HomeModuleCatalog.Move(_modules, index, -1);
                RebuildModuleGrid();
            };
            right.Clicked += (_, _) =>
            {
                HomeModuleCatalog.Move(_modules, index, +1);
                RebuildModuleGrid();
            };
            body.Children.Add(new HorizontalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                Children = { left, right }
            });
        }

        var border = new Border
        {
            WidthRequest = 100,
            Margin = new Thickness(4),
            Padding = 0,
            Content = body
        };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");

        if (!_editMode)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                try { await Shell.Current.GoToAsync(module.Route); }
                catch { _status.Text = $"Could not open {module.Title}."; }
            };
            border.GestureRecognizers.Add(tap);

            var longPress = new SwipeGestureRecognizer { Direction = SwipeDirection.Down };
            // Long-press via press duration is limited in pure MAUI; Edit button is primary.
        }

        return border;
    }

    private static View PulseCard(string eyebrow, string body, string? meta, View? action)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        var eye = new Label { Text = eyebrow.ToUpperInvariant(), FontSize = 11, FontAttributes = FontAttributes.Bold };
        eye.SetDynamicResource(Label.TextColorProperty, "AccentSky");
        stack.Children.Add(eye);
        var main = new Label { Text = body, FontSize = 16, LineBreakMode = LineBreakMode.WordWrap };
        main.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        stack.Children.Add(main);
        if (!string.IsNullOrWhiteSpace(meta))
        {
            var m = new Label { Text = meta, FontSize = 12 };
            m.SetDynamicResource(Label.TextColorProperty, "TextMuted");
            stack.Children.Add(m);
        }
        if (action is not null) stack.Children.Add(action);
        var border = new Border { Padding = 18, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return border;
    }

    private static View MiniStat(int value, string label)
    {
        var v = new Label
        {
            Text = value.ToString(),
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };
        v.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var l = new Label { Text = label, FontSize = 11, HorizontalTextAlignment = TextAlignment.Center };
        l.SetDynamicResource(Label.TextColorProperty, "TextMuted");
        var border = new Border
        {
            Padding = new Thickness(6, 12),
            Content = new VerticalStackLayout { Spacing = 2, Children = { v, l } }
        };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return border;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, Margin = new Thickness(2, 8, 0, 0) };
        label.SetDynamicResource(Label.TextColorProperty, "TextMuted");
        return label;
    }

    private static string FirstName(string displayName)
    {
        var part = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(part) ? displayName : part;
    }

    private static async Task OpenKindAsync(string kind)
    {
        var route = kind.Trim().ToLowerInvariant() switch
        {
            "request" => nameof(RequestsPage),
            "event" or "calendar" => nameof(CalendarPage),
            "meal" or "recipe" => nameof(MealsPage),
            "message" or "chat" => nameof(ChatPage),
            "notification" => nameof(NotificationsPage),
            "family" or "member" or "shopping" or "procurement" => nameof(FamilyPage),
            "location" or "map" => "//map",
            _ => nameof(TasksPage)
        };
        await Shell.Current.GoToAsync(route);
    }
}
