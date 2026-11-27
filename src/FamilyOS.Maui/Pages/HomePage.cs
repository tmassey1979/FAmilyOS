using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Household home: presence strip, tactical Pulse banner, rearrangeable module grid.
/// Merge-2 sci-fi command chrome (deep charcoal + cyan accents).
/// </summary>
public sealed class HomePage : ContentPage
{
    private readonly PulseApiClient _pulse;
    private readonly FamilyApiClient _family;
    private readonly TasksApiClient _tasks;
    private readonly PulseRefreshSignal _pulseRefresh;
    private readonly HorizontalStackLayout _members = new() { Spacing = 14 };
    private readonly Label _greeting = new() { FontSize = 28, FontAttributes = FontAttributes.Bold };
    private readonly Label _status = new() { FontSize = 14 };
    private readonly VerticalStackLayout _pulseSection = new() { Spacing = 10 };
    private readonly Grid _moduleGrid = new()
    {
        ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star)
        },
        ColumnSpacing = 12,
        RowSpacing = 12
    };
    private readonly Button _editToggle = new() { Text = "Edit" };
    private List<HomeModule> _modules = [];
    private bool _editMode;
    private bool _loading;
    private Guid? _selectedMemberId;

    public HomePage(PulseApiClient pulse, FamilyApiClient family, TasksApiClient tasks, PulseRefreshSignal pulseRefresh)
    {
        _pulse = pulse;
        _family = family;
        _tasks = tasks;
        _pulseRefresh = pulseRefresh;
        _pulseRefresh.RefreshRequested += OnPulseRefreshRequested;
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

        var brand = new Label
        {
            Text = "◈  FAMILY OS",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.4
        };
        brand.SetDynamicResource(Label.TextColorProperty, "AccentGlow");

        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        topRow.Add(brand, 0, 0);
        topRow.Add(_editToggle, 1, 0);

        var accent = new BoxView { HeightRequest = 1.5, Opacity = 0.55 };
        accent.SetDynamicResource(BoxView.ColorProperty, "AccentPrimary");

        var page = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12, 20, 28),
            Spacing = 14,
            Children =
            {
                topRow,
                accent,
                membersScroll,
                _greeting,
                _status,
                _pulseSection,
                SectionLabel("COMMAND MODULES"),
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

    private void OnPulseRefreshRequested(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loading) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try { await Task.WhenAll(LoadMembersAsync(), LoadPulseAsync()); }
        finally { _loading = false; }
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
        catch { /* optional */ }
    }

    private View BuildMemberChip(FamilyMemberDto member, bool isSelf)
    {
        var initials = string.Join("", member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(p => char.ToUpperInvariant(p[0])));
        if (string.IsNullOrEmpty(initials)) initials = "?";

        var selected = _selectedMemberId == member.Id;
        var avatar = new Border
        {
            WidthRequest = 56,
            HeightRequest = 56,
            StrokeThickness = selected ? 2.5 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 28 },
            Content = new Label
            {
                Text = initials,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        avatar.SetDynamicResource(Border.StrokeProperty, selected ? "AccentPrimary" : "BorderSubtle");
        avatar.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurfaceAlt");
        if (avatar.Content is Label avatarLabel)
            avatarLabel.SetDynamicResource(Label.TextColorProperty, selected ? "AccentGlow" : "TextPrimary");

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
            if (member.Id != default)
            {
                try { await Shell.Current.GoToAsync($"//map?memberId={member.Id}"); }
                catch { /* map optional */ }
            }
        };
        stack.GestureRecognizers.Add(tap);
        return stack;
    }

    private async Task LoadPulseAsync()
    {
        _status.Text = "Syncing household…";
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
                btn.Clicked += async (_, _) => await HandleNextActionAsync(next);
                _pulseSection.Children.Add(PulseCard("NEXT UP", next.Title, next.DueLabel, btn, accent: true));
            }
            else
            {
                _pulseSection.Children.Add(PulseCard("PULSE", "You're mostly clear — nothing urgent right now.", null, null, accent: false));
            }

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
        _moduleGrid.RowDefinitions.Clear();
        const int cols = 2;
        var rows = (_modules.Count + cols - 1) / cols;
        for (var r = 0; r < rows; r++)
            _moduleGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var i = 0; i < _modules.Count; i++)
            _moduleGrid.Add(BuildModuleTile(_modules[i], i), i % cols, i / cols);
    }

    private View BuildModuleTile(HomeModule module, int index)
    {
        var glyph = new Label
        {
            Text = module.Glyph,
            FontSize = 26,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var iconWell = new Border
        {
            WidthRequest = 56,
            HeightRequest = 56,
            StrokeThickness = 1,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = glyph
        };
        iconWell.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurfaceAlt");
        iconWell.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        glyph.SetDynamicResource(Label.TextColorProperty, "AccentPrimary");

        var title = new Label
        {
            Text = module.Title,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

        var body = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(10, 14),
            Children = { iconWell, title }
        };

        if (_editMode)
        {
            var left = new Button { Text = "\u2190", WidthRequest = 36, FontSize = 14 };
            var right = new Button { Text = "\u2192", WidthRequest = 36, FontSize = 14 };
            left.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            right.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            left.Clicked += (_, _) => { HomeModuleCatalog.Move(_modules, index, -1); RebuildModuleGrid(); };
            right.Clicked += (_, _) => { HomeModuleCatalog.Move(_modules, index, +1); RebuildModuleGrid(); };
            body.Children.Add(new HorizontalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                Children = { left, right }
            });
        }

        var border = new Border { Padding = 0, MinimumHeightRequest = 124, Content = body };
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
        }

        return border;
    }

    private static View PulseCard(string eyebrow, string body, string? meta, View? action, bool accent)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        var eye = new Label { Text = eyebrow, FontSize = 11, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.2 };
        eye.SetDynamicResource(Label.TextColorProperty, "AccentGlow");
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

        var rail = new BoxView { WidthRequest = 3 };
        rail.SetDynamicResource(BoxView.ColorProperty, accent ? "AccentPrimary" : "BorderSubtle");
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };
        row.Add(rail, 0, 0);
        row.Add(stack, 1, 0);

        var border = new Border { Padding = 16, Content = row };
        border.SetDynamicResource(VisualElement.StyleProperty, accent ? "FoCardAccentFrameStyle" : "FoCardFrameStyle");
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
        v.SetDynamicResource(Label.TextColorProperty, value > 0 ? "AccentGlow" : "TextPrimary");
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
        var label = new Label
        {
            Text = text,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.4,
            Margin = new Thickness(2, 8, 0, 0)
        };
        label.SetDynamicResource(Label.TextColorProperty, "AccentGlow");
        return label;
    }

    private static string FirstName(string displayName)
    {
        var part = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(part) ? displayName : part;
    }

    private async Task HandleNextActionAsync(PulseNextActionDto next)
    {
        if (!string.Equals(next.Kind, "Task", StringComparison.OrdinalIgnoreCase))
        {
            await OpenKindAsync(next.Kind);
            return;
        }

        var action = next.PrimaryAction.Trim().ToLowerInvariant() switch
        {
            "accept" => "accept",
            "start" => "start",
            "resume" => "resume",
            "complete" => "complete",
            _ => null
        };

        if (action is null)
        {
            await Shell.Current.GoToAsync($"{nameof(TasksPage)}?scope=family");
            return;
        }

        try
        {
            await _tasks.RunActionAsync(next.EntityId, action);
            _status.Text = action == "complete" ? "Task completed." : "Task updated.";
            _pulseRefresh.RequestRefresh();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch { _status.Text = "Could not update that task. Open Tasks to try again."; }
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
