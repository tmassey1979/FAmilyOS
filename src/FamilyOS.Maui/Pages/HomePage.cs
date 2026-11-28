using FamilyOS.Maui.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Merge-2 household command center: presence strip, tactical Pulse panel,
/// stat strip, rearrangeable module grid — deep charcoal + cyan chrome.
/// </summary>
public sealed class HomePage : ContentPage
{
    private readonly PulseApiClient _pulse;
    private readonly FamilyApiClient _family;
    private readonly TasksApiClient _tasks;
    private readonly PulseRefreshSignal _pulseRefresh;
    private readonly HorizontalStackLayout _members = new() { Spacing = 12 };
    private readonly Label _greeting = new() { FontSize = 24, FontAttributes = FontAttributes.Bold };
    private readonly Label _status = new() { FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
    private readonly VerticalStackLayout _pulseSection = new() { Spacing = 10 };
    private readonly Grid _moduleGrid = new()
    {
        ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star)
        },
        ColumnSpacing = 10,
        RowSpacing = 10
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
            Content = _members,
            HeightRequest = 78
        };

        // Brand row — crest + wordmark + edit
        var crest = new Label
        {
            Text = "◈",
            FontSize = 16,
            VerticalTextAlignment = TextAlignment.Center
        };
        crest.SetDynamicResource(Label.TextColorProperty, "AccentPrimary");

        var brand = new Label
        {
            Text = "FAMILY OS",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 2.0,
            VerticalTextAlignment = TextAlignment.Center
        };
        brand.SetDynamicResource(Label.TextColorProperty, "AccentGlow");

        var brandRow = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children = { crest, brand }
        };

        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(0, 2, 0, 0)
        };
        topRow.Add(brandRow, 0, 0);
        topRow.Add(_editToggle, 1, 0);

        // Cyan scan line under brand
        var scanLine = new BoxView { HeightRequest = 2, Opacity = 0.85 };
        scanLine.SetDynamicResource(BoxView.ColorProperty, "AccentPrimary");

        // Greeting block sits inside a soft surface panel for weight
        var greetingPanel = BuildGreetingPanel();

        var page = new VerticalStackLayout
        {
            Padding = new Thickness(16, 8, 16, 24),
            Spacing = 12,
            Children =
            {
                topRow,
                scanLine,
                membersScroll,
                greetingPanel,
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

    private View BuildGreetingPanel()
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 4,
            Children = { _greeting, _status }
        };

        // Left cyan bracket + surface — tactical weight without a full card border
        var rail = new BoxView { WidthRequest = 3 };
        rail.SetDynamicResource(BoxView.ColorProperty, "AccentPrimary");

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

        var border = new Border
        {
            Padding = new Thickness(14, 12),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = row
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurface");
        border.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        return border;
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
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeThickness = selected ? 2 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            Content = new Label
            {
                Text = initials,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        avatar.SetDynamicResource(Border.StrokeProperty, selected ? "AccentPrimary" : "BorderSubtle");
        avatar.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurfaceAlt");
        if (avatar.Content is Label avatarLabel)
            avatarLabel.SetDynamicResource(Label.TextColorProperty, selected ? "AccentGlow" : "TextPrimary");

        // Online-style presence dot (self = cyan, others = muted success)
        var dot = new BoxView
        {
            WidthRequest = 10,
            HeightRequest = 10,
            CornerRadius = 5,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, -1, -1)
        };
        dot.SetDynamicResource(BoxView.ColorProperty, isSelf || selected ? "AccentPrimary" : "AccentSuccess");

        var avatarHost = new Grid
        {
            WidthRequest = 52,
            HeightRequest = 52,
            HorizontalOptions = LayoutOptions.Center
        };
        avatarHost.Add(avatar);
        avatarHost.Add(dot);

        var name = new Label
        {
            Text = isSelf ? "You" : FirstName(member.DisplayName),
            FontSize = 11,
            FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        name.SetDynamicResource(Label.TextColorProperty, selected ? "AccentGlow" : "TextMuted");

        var stack = new VerticalStackLayout
        {
            Spacing = 4,
            WidthRequest = 60,
            Children = { avatarHost, name }
        };

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
                var actionLabel = string.IsNullOrWhiteSpace(next.PrimaryAction)
                    ? "Open"
                    : next.PrimaryAction.Trim();
                // Title-case the verb for the CTA; keep task name in the card body only
                if (actionLabel.Length > 0)
                    actionLabel = char.ToUpperInvariant(actionLabel[0]) + actionLabel[1..].ToLowerInvariant();

                var btn = new Button
                {
                    Text = actionLabel,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 15,
                    CornerRadius = 14,
                    MinimumHeightRequest = 48,
                    Padding = new Thickness(20, 0)
                };
                btn.SetDynamicResource(Button.BackgroundColorProperty, "AccentPrimary");
                btn.SetDynamicResource(Button.TextColorProperty, "TextOnAccent");
                btn.Clicked += async (_, _) => await HandleNextActionAsync(next);

                var isOverdue = !string.IsNullOrWhiteSpace(next.DueLabel)
                    && next.DueLabel.Contains("overdue", StringComparison.OrdinalIgnoreCase);

                _pulseSection.Children.Add(PulseCard(
                    "NEXT UP",
                    next.Title,
                    next.DueLabel,
                    btn,
                    accent: true,
                    overdue: isOverdue));
            }
            else
            {
                _pulseSection.Children.Add(PulseCard(
                    "PULSE",
                    "You're mostly clear — nothing urgent right now.",
                    null,
                    null,
                    accent: false,
                    overdue: false));
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
            FontSize = 22,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        glyph.SetDynamicResource(Label.TextColorProperty, "AccentPrimary");

        var iconWell = new Border
        {
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeThickness = 1,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = glyph
        };
        iconWell.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgPage");
        iconWell.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");

        var title = new Label
        {
            Text = module.Title,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

        var body = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(12, 16),
            Children = { iconWell, title }
        };

        if (_editMode)
        {
            var left = new Button { Text = "←", WidthRequest = 36, FontSize = 14 };
            var right = new Button { Text = "→", WidthRequest = 36, FontSize = 14 };
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

        var border = new Border
        {
            Padding = 0,
            MinimumHeightRequest = 112,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = body
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurface");
        border.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");

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

    private static View PulseCard(string eyebrow, string body, string? meta, View? action, bool accent, bool overdue)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var eye = new Label
        {
            Text = eyebrow,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.4,
            VerticalTextAlignment = TextAlignment.Center
        };
        eye.SetDynamicResource(Label.TextColorProperty, "AccentGlow");
        header.Add(eye, 0, 0);

        if (overdue)
        {
            var pillLabel = new Label
            {
                Text = "OVERDUE",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            pillLabel.SetDynamicResource(Label.TextColorProperty, "TextOnAccent");
            var pill = new Border
            {
                Padding = new Thickness(8, 3),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                Content = pillLabel
            };
            pill.SetDynamicResource(VisualElement.BackgroundColorProperty, "AccentDanger");
            header.Add(pill, 1, 0);
        }

        stack.Children.Add(header);

        var main = new Label
        {
            Text = body,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        };
        main.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        stack.Children.Add(main);

        if (!string.IsNullOrWhiteSpace(meta) && !overdue)
        {
            var m = new Label { Text = meta, FontSize = 12 };
            m.SetDynamicResource(Label.TextColorProperty, "TextMuted");
            stack.Children.Add(m);
        }
        else if (!string.IsNullOrWhiteSpace(meta) && overdue)
        {
            // Still show due context under title when overdue pill is present
            var m = new Label { Text = meta, FontSize = 12 };
            m.SetDynamicResource(Label.TextColorProperty, "AccentDanger");
            stack.Children.Add(m);
        }

        if (action is not null)
            stack.Children.Add(action);

        var rail = new BoxView { WidthRequest = 4 };
        rail.SetDynamicResource(BoxView.ColorProperty, overdue ? "AccentDanger" : accent ? "AccentPrimary" : "BorderSubtle");

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

        var border = new Border
        {
            Padding = new Thickness(14, 14),
            StrokeThickness = accent || overdue ? 1.5 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = row
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurface");
        border.SetDynamicResource(Border.StrokeProperty,
            overdue ? "AccentDanger" : accent ? "AccentPrimary" : "BorderSubtle");
        return border;
    }

    private static View MiniStat(int value, string label)
    {
        var hot = value > 0;

        var v = new Label
        {
            Text = value.ToString(),
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };
        v.SetDynamicResource(Label.TextColorProperty, hot ? "AccentGlow" : "TextPrimary");

        var l = new Label
        {
            Text = label,
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center
        };
        l.SetDynamicResource(Label.TextColorProperty, "TextMuted");

        var border = new Border
        {
            Padding = new Thickness(6, 12),
            StrokeThickness = hot ? 1.5 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = new VerticalStackLayout { Spacing = 2, Children = { v, l } }
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurface");
        border.SetDynamicResource(Border.StrokeProperty, hot ? "AccentPrimary" : "BorderSubtle");
        return border;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.6,
            Margin = new Thickness(2, 4, 0, 0)
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
