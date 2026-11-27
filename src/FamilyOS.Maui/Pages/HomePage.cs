using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Household home shell: member strip, Pulse command center, and rearrangeable module grid.
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

    private void OnPulseRefreshRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
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
        }
    }

    private View BuildMemberChip(FamilyMemberDto member, bool isSelf)
    {
        var initials = string.Join("", member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(p => char.ToUpperInvariant(p[0])));
        if (string.IsNullOrEmpty(initials)) initials = "?";

        var avatar = new Border
        {
            WidthRequest = 56,
            HeightRequest = 56,
            StrokeThickness = _selectedMemberId == member.Id ? 3 : 0,
            
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 26 },
            Content = new Label
            {
                Text = initials,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                
            }
        };
        avatar.SetDynamicResource(Border.StrokeProperty, "AccentPrimary");
        if (avatar.Content is Label avatarLabel)
            avatarLabel.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        avatar.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgGlow");

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
                await Shell.Current.GoToAsync($"//map?memberId={member.Id}");
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
                btn.Clicked += async (_, _) => await HandleNextActionAsync(next);
                _pulseSection.Children.Add(PulseCard("Next up", next.Subtitle ?? next.Title, next.DueLabel, btn));
            }
            else
            {
                _pulseSection.Children.Add(PulseCard("Pulse", "You're caught up — nothing urgent right now.", null, null));
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
            VerticalTextAlignment = TextAlignment.Center,
            
        };

        var iconWell = new Border
        {
            WidthRequest = 56,
            HeightRequest = 56,
            
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = glyph
        };
        iconWell.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgGlow");
        iconWell.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        iconWell.StrokeThickness = 1;
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

    private static View PulseCard(string eyebrow, string body, string? meta, View? action)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        var eye = new Label { Text = eyebrow.ToUpperInvariant(), FontSize = 11, FontAttributes = FontAttributes.Bold };
        eye.SetDynamicResource(Label.TextColorProperty, "AccentPrimary");
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
            await Shell.Current.GoToAsync(nameof(TasksPage));
            return;
        }

        try
        {
            await _tasks.RunActionAsync(next.EntityId, action);
            _status.Text = action == "complete" ? "Task completed." : "Task updated.";
            _pulseRefresh.RequestRefresh();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not update that task. Open Tasks to try again.";
        }
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
