using FamilyOS.Maui.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Merge-2 target layout: crest header, presence row, STATUS panel with
/// cyan L-brackets + NEXT UP, stat strip, badged 2-col command modules.
/// </summary>
public sealed class HomePage : ContentPage
{
    private readonly PulseApiClient _pulse;
    private readonly FamilyApiClient _family;
    private readonly TasksApiClient _tasks;
    private readonly PulseRefreshSignal _pulseRefresh;

    private readonly HorizontalStackLayout _members = new() { Spacing = 14 };
    private readonly Label _greeting = new()
    {
        FontSize = 26,
        FontAttributes = FontAttributes.Bold,
        LineBreakMode = LineBreakMode.WordWrap
    };
    private readonly Label _roleHint = new()
    {
        FontSize = 13,
        LineBreakMode = LineBreakMode.WordWrap
    };
    private readonly VerticalStackLayout _nextUpHost = new() { Spacing = 8 };
    private readonly Grid _statsGrid = new()
    {
        ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star)
        },
        ColumnSpacing = 8
    };
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
    private Dictionary<string, int> _moduleBadges = new(StringComparer.OrdinalIgnoreCase);
    private bool _editMode;
    private bool _loading;
    private Guid? _selectedMemberId;

    public HomePage(
        PulseApiClient pulse,
        FamilyApiClient family,
        TasksApiClient tasks,
        PulseRefreshSignal pulseRefresh)
    {
        _pulse = pulse;
        _family = family;
        _tasks = tasks;
        _pulseRefresh = pulseRefresh;
        _pulseRefresh.RefreshRequested += OnPulseRefreshRequested;
        Title = "Home";

        _greeting.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        _roleHint.SetDynamicResource(Label.TextColorProperty, "TextMuted");
        _editToggle.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _editToggle.Clicked += (_, _) => ToggleEditMode();

        Content = new RefreshView
        {
            Content = new ScrollView { Content = BuildPage() },
            Command = new Command(async () => await LoadAsync())
        };

        _modules = HomeModuleCatalog.GetOrdered();
        RebuildModuleGrid();
    }

    private View BuildPage()
    {
        var crest = new Label
        {
            Text = "◈",
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

    /// <summary>STATUS panel with cyan L-corner brackets + optional NEXT UP block.</summary>
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

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12,
            Padding = new Thickness(12, 14, 14, 14)
        };
        content.Add(rail, 0, 0);
        content.Add(body, 1, 0);

        // Surface card
        var surface = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = content
        };
        surface.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurface");
        surface.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");

        // L-brackets: top-left and bottom-right cyan corners (Merge-2 signature)
        var bracketColor = Color.FromArgb("#22D3EE");
        var tlH = new BoxView { Color = bracketColor, HeightRequest = 2, WidthRequest = 22, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var tlV = new BoxView { Color = bracketColor, WidthRequest = 2, HeightRequest = 22, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var brH = new BoxView { Color = bracketColor, HeightRequest = 2, WidthRequest = 22, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End };
        var brV = new BoxView { Color = bracketColor, WidthRequest = 2, HeightRequest = 22, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End };

        var host = new Grid { Padding = 0 };
        host.Add(surface);
        host.Add(tlH);
        host.Add(tlV);
        host.Add(brH);
        host.Add(brV);
        return host;
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
        catch { /* optional strip */ }
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
            StrokeThickness = selected ? 2.5 : 1.5,
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
        if (avatar.Content is Label al)
            al.SetDynamicResource(Label.TextColorProperty, selected ? "AccentGlow" : "TextPrimary");

        var dot = new BoxView
        {
            WidthRequest = 11,
            HeightRequest = 11,
            CornerRadius = 6,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 0)
        };
        dot.SetDynamicResource(BoxView.ColorProperty, isSelf || selected ? "AccentPrimary" : "AccentSuccess");

        // Outer ring so the presence dot sits on the avatar corner cleanly
        var avatarHost = new Grid { WidthRequest = 52, HeightRequest = 52, HorizontalOptions = LayoutOptions.Center };
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

        var stack = new VerticalStackLayout { Spacing = 4, WidthRequest = 60, Children = { avatarHost, name } };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            _selectedMemberId = member.Id;
            await LoadMembersAsync();
            _roleHint.Text = $"Focused on {member.DisplayName}";
            try { await Shell.Current.GoToAsync($"//map?memberId={member.Id}"); }
            catch { /* map optional */ }
        };
        stack.GestureRecognizers.Add(tap);
        return stack;
    }

    private async Task LoadPulseAsync()
    {
        _roleHint.Text = "Syncing household…";
        _nextUpHost.Children.Clear();
        _statsGrid.Children.Clear();
        try
        {
            var pulse = await _pulse.GetPulseAsync();
            _greeting.Text = string.IsNullOrWhiteSpace(pulse.Greeting) ? "Welcome home" : pulse.Greeting;
            _roleHint.Text = pulse.RoleHint ?? "Your household at a glance.";

            // Module badge counts from live pulse
            _moduleBadges = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["tasks"] = pulse.Household.TasksDueToday + pulse.Household.TasksNeedingAcceptance,
                ["requests"] = pulse.Household.RequestsAwaitingApproval,
            };
            RebuildModuleGrid();

            if (pulse.NextAction is not null)
            {
                var next = pulse.NextAction;
                var isOverdue = !string.IsNullOrWhiteSpace(next.DueLabel)
                    && next.DueLabel.Contains("overdue", StringComparison.OrdinalIgnoreCase);

                var nextEyebrow = new Label
                {
                    Text = "NEXT UP",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    CharacterSpacing = 1.4
                };
                nextEyebrow.SetDynamicResource(Label.TextColorProperty, "AccentGlow");

                var titleRow = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 8
                };
                var title = new Label
                {
                    Text = next.Title,
                    FontSize = 17,
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.WordWrap,
                    VerticalTextAlignment = TextAlignment.Center
                };
                title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
                titleRow.Add(title, 0, 0);
                if (isOverdue)
                    titleRow.Add(StatusPill("OVERDUE", "danger"), 1, 0);

                var actionText = string.IsNullOrWhiteSpace(next.PrimaryAction)
                    ? "Open"
                    : next.PrimaryAction.Trim();
                if (actionText.Length > 0)
                    actionText = char.ToUpperInvariant(actionText[0]) + actionText[1..].ToLowerInvariant();

                var btn = new Button
                {
                    Text = actionText,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 15,
                    CornerRadius = 14,
                    MinimumHeightRequest = 48,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                btn.SetDynamicResource(Button.BackgroundColorProperty, "AccentPrimary");
                btn.SetDynamicResource(Button.TextColorProperty, "TextOnAccent");
                btn.Clicked += async (_, _) => await HandleNextActionAsync(next);

                _nextUpHost.Children.Add(new BoxView { HeightRequest = 1, Opacity = 0.2, Margin = new Thickness(0, 4, 0, 4) }.Also(b => b.SetDynamicResource(BoxView.ColorProperty, "BorderSubtle")));
                _nextUpHost.Children.Add(nextEyebrow);
                _nextUpHost.Children.Add(titleRow);
                if (!string.IsNullOrWhiteSpace(next.DueLabel) && !isOverdue)
                {
                    var meta = new Label { Text = next.DueLabel, FontSize = 12 };
                    meta.SetDynamicResource(Label.TextColorProperty, "TextMuted");
                    _nextUpHost.Children.Add(meta);
                }
                _nextUpHost.Children.Add(btn);
            }
            else
            {
                var clear = new Label
                {
                    Text = "You're mostly clear — nothing urgent right now.",
                    FontSize = 14,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                clear.SetDynamicResource(Label.TextColorProperty, "TextMuted");
                _nextUpHost.Children.Add(clear);
            }

            _statsGrid.Add(MiniStat(pulse.Household.TasksDueToday, "Due"), 0, 0);
            _statsGrid.Add(MiniStat(pulse.Household.RequestsAwaitingApproval, "Approvals"), 1, 0);
            _statsGrid.Add(MiniStat(pulse.Household.TasksNeedingAcceptance, "Accept"), 2, 0);
        }
        catch (FamilyApiException ex)
        {
            _roleHint.Text = ex.Message;
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                var signIn = new Button { Text = "Sign in / set up household" };
                signIn.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
                signIn.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(OnboardingPage));
                _nextUpHost.Children.Add(signIn);
            }
        }
        catch
        {
            _roleHint.Text = "Could not load Pulse. Pull to refresh.";
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
        _moduleBadges.TryGetValue(module.Id, out var badgeCount);
        var hasBadge = badgeCount > 0;

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
        iconWell.SetDynamicResource(Border.StrokeProperty, hasBadge ? "AccentPrimary" : "BorderSubtle");

        View top = iconWell;
        if (hasBadge)
        {
            var badge = StatusPill(badgeCount > 99 ? "99+" : badgeCount.ToString(), badgeCount > 2 ? "danger" : "info");
            badge.HorizontalOptions = LayoutOptions.End;
            badge.VerticalOptions = LayoutOptions.Start;
            var topGrid = new Grid { WidthRequest = 56, HeightRequest = 52, HorizontalOptions = LayoutOptions.Center };
            topGrid.Add(iconWell);
            topGrid.Add(badge);
            top = topGrid;
        }

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
            Children = { top, title }
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
            StrokeThickness = hasBadge ? 1.5 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = body
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurface");
        border.SetDynamicResource(Border.StrokeProperty, hasBadge ? "AccentPrimary" : "BorderSubtle");

        if (!_editMode)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                try { await Shell.Current.GoToAsync(module.Route); }
                catch { _roleHint.Text = $"Could not open {module.Title}."; }
            };
            border.GestureRecognizers.Add(tap);
        }

        return border;
    }

    private static View StatusPill(string text, string tone)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        label.SetDynamicResource(Label.TextColorProperty, tone == "danger" ? "TextOnAccent" : "TextOnAccent");

        var border = new Border
        {
            Padding = new Thickness(8, 3),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Content = label
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty,
            tone == "danger" ? "AccentDanger" : "AccentPrimary");
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
            Margin = new Thickness(2, 2, 0, 0)
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
            _roleHint.Text = action == "complete" ? "Task completed." : "Task updated.";
            _pulseRefresh.RequestRefresh();
        }
        catch (FamilyApiException ex) { _roleHint.Text = ex.Message; }
        catch { _roleHint.Text = "Could not update that task. Open Tasks to try again."; }
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

internal static class ViewExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
