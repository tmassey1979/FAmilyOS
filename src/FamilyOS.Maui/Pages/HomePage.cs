using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>API-backed Family Pulse dashboard. All household data is server-authorized.</summary>
public sealed class HomePage : ContentPage
{
    private readonly PulseApiClient _pulseClient;
    private readonly VerticalStackLayout _content;
    private readonly Label _greeting;
    private readonly Label _status;
    private readonly Label _roleChip;
    private readonly Button _refresh;
    private PulseDto? _snapshot;
    private bool _loading;

    public HomePage(PulseApiClient pulseClient)
    {
        _pulseClient = pulseClient;
        Title = "Pulse";

        var brand = new HorizontalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Image
                {
                    Source = "logo_mark.png",
                    WidthRequest = 28,
                    HeightRequest = 28,
                    Aspect = Aspect.AspectFit
                },
                new Image
                {
                    Source = "logo_wordmark.png",
                    HeightRequest = 20,
                    Aspect = Aspect.AspectFit
                }
            }
        };

        _refresh = new Button { Text = "Refresh" };
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();

        var topBar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(0, 0, 0, 4)
        };
        topBar.Add(brand, 0, 0);
        topBar.Add(_refresh, 1, 0);

        _greeting = new Label
        {
            Text = "Welcome back",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _greeting.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

        _roleChip = new Label
        {
            Text = "",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(10, 4),
            HorizontalOptions = LayoutOptions.Start
        };
        _roleChip.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        _roleChip.SetDynamicResource(Label.BackgroundColorProperty, "AccentSoft");

        _status = new Label { Text = "Loading your family dashboard…", FontSize = 14, Margin = new Thickness(0, 4, 0, 0) };
        _status.SetDynamicResource(Label.TextColorProperty, "TextMuted");

        _content = new VerticalStackLayout { Spacing = 14 };

        var page = new VerticalStackLayout
        {
            Padding = new Thickness(20, 16, 20, 28),
            Spacing = 10,
            Children = { topBar, _greeting, _roleChip, _status, _content }
        };

        Content = new RefreshView
        {
            Content = new ScrollView { Content = page },
            Command = new Command(async () => await LoadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_snapshot is null && !_loading)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _refresh.IsEnabled = false;
        _status.Text = _snapshot is null ? "Loading your family dashboard…" : "Refreshing…";
        try
        {
            _snapshot = await _pulseClient.GetPulseAsync();
            Render(_snapshot);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "The request timed out. Check your connection and try again.";
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                AddSignInHint();
        }
        catch (HttpRequestException)
        {
            _status.Text = "FamilyOS could not be reached. Check your connection and try again.";
        }
        catch (Exception)
        {
            _status.Text = "Something went wrong while loading Family Pulse.";
        }
        finally
        {
            _loading = false;
            _refresh.IsEnabled = true;
        }
    }

    private void Render(PulseDto pulse)
    {
        _content.Children.Clear();
        _greeting.Text = string.IsNullOrWhiteSpace(pulse.Greeting) ? "Family Pulse" : pulse.Greeting;
        _roleChip.Text = string.IsNullOrWhiteSpace(pulse.Role) ? "" : pulse.Role.ToUpperInvariant();
        _roleChip.IsVisible = !string.IsNullOrWhiteSpace(pulse.Role);
        _status.Text = pulse.RoleHint ?? "Your household at a glance.";

        if (pulse.NextAction is not null)
        {
            var next = pulse.NextAction;
            var action = new Button
            {
                Text = string.IsNullOrWhiteSpace(next.PrimaryAction)
                    ? next.Title
                    : $"{next.PrimaryAction}: {next.Title}",
                HorizontalOptions = LayoutOptions.Fill
            };
            action.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
            action.Clicked += async (_, _) => await NavigateForAsync(next.Kind);
            AddHeroCard("Next up", next.Subtitle, action, next.DueLabel);
        }

        AddSummary(pulse.Household);

        if (pulse.NeedsAttention.Count > 0)
            AddAttentionSection("Needs attention", pulse.NeedsAttention);

        if (pulse.WaitingBlockers.Count > 0)
            AddAttentionSection("Waiting & blockers", pulse.WaitingBlockers);

        if (pulse.ComingUp.Count > 0)
        {
            AddSectionTitle("Coming up");
            foreach (var item in pulse.ComingUp)
            {
                var when = item.WhenUtc.ToLocalTime().ToString("ddd, MMM d · h:mm tt");
                AddCard(item.Title, $"{when}{(string.IsNullOrWhiteSpace(item.Location) ? "" : $" · {item.Location}")}", null, item.Kind);
            }
        }

        if (pulse.Procurement is not null)
        {
            AddSectionTitle("Shopping");
            AddCard($"{pulse.Procurement.ApprovedItems} items approved",
                $"{pulse.Procurement.ReadyForStore} ready for a store trip",
                null, pulse.Procurement.PrimaryStore);
        }

        if (pulse.Conditions.Count > 0)
        {
            AddSectionTitle("Open conditions");
            foreach (var condition in pulse.Conditions)
                AddCard(condition.Name, condition.RelatedTitle, null, condition.Status);
        }

        if (pulse.NeedsAttention.Count == 0 && pulse.WaitingBlockers.Count == 0
            && pulse.ComingUp.Count == 0 && pulse.NextAction is null)
        {
            AddCard("You’re all caught up", "There are no urgent actions or upcoming items right now.", null, "All clear");
        }
    }

    private void AddSummary(PulseHouseholdSummaryDto household)
    {
        AddSectionTitle("At a glance");
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        grid.Add(StatCard(household.TasksDueToday, "Due today"), 0, 0);
        grid.Add(StatCard(household.RequestsAwaitingApproval, "Approvals"), 1, 0);
        grid.Add(StatCard(household.TasksNeedingAcceptance, "To accept"), 2, 0);
        _content.Children.Add(grid);
    }

    private void AddAttentionSection(string title, IEnumerable<PulseAttentionItemDto> items)
    {
        AddSectionTitle(title);
        foreach (var item in items)
        {
            var button = new Button { Text = item.ActionLabel, FontSize = 13, HorizontalOptions = LayoutOptions.Start };
            button.SetDynamicResource(VisualElement.StyleProperty, "FoChipButtonStyle");
            button.Clicked += async (_, _) => await NavigateForAsync(item.Kind);
            AddCard(item.Title, item.Reason, button, item.Kind);
        }
    }

    private void AddHeroCard(string title, string? subtitle, View? action, string? meta)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        var eyebrow = new Label { Text = title.ToUpperInvariant(), FontSize = 11, FontAttributes = FontAttributes.Bold };
        eyebrow.SetDynamicResource(Label.TextColorProperty, "AccentSky");
        stack.Children.Add(eyebrow);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var detail = new Label { Text = subtitle, FontSize = 16, LineBreakMode = LineBreakMode.WordWrap };
            detail.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
            stack.Children.Add(detail);
        }
        if (!string.IsNullOrWhiteSpace(meta))
        {
            var caption = new Label { Text = meta, FontSize = 12 };
            caption.SetDynamicResource(Label.TextColorProperty, "TextMuted");
            stack.Children.Add(caption);
        }
        if (action is not null)
            stack.Children.Add(action);

        var border = new Border { Padding = 20, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardElevatedStyle");
        border.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        _content.Children.Add(border);
    }

    private void AddCard(string title, string? subtitle, View? action, string? meta)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        var heading = new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        stack.Children.Add(heading);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var detail = new Label { Text = subtitle, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
            detail.SetDynamicResource(Label.TextColorProperty, "TextMuted");
            stack.Children.Add(detail);
        }
        if (!string.IsNullOrWhiteSpace(meta))
        {
            var caption = new Label
            {
                Text = meta,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(8, 3),
                HorizontalOptions = LayoutOptions.Start
            };
            caption.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
            caption.SetDynamicResource(Label.BackgroundColorProperty, "BgSurfaceAlt");
            stack.Children.Add(caption);
        }
        if (action is not null) stack.Children.Add(action);

        var border = new Border { Padding = 16, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _content.Children.Add(border);
    }

    private View StatCard(int value, string label)
    {
        var valueLabel = new Label
        {
            Text = value.ToString(),
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };
        valueLabel.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

        var caption = new Label
        {
            Text = label,
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center
        };
        caption.SetDynamicResource(Label.TextColorProperty, "TextMuted");

        var stack = new VerticalStackLayout { Spacing = 4, Children = { valueLabel, caption } };
        var border = new Border { Padding = new Thickness(8, 14), Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return border;
    }

    private void AddSectionTitle(string title)
    {
        var label = new Label
        {
            Text = title,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(2, 10, 0, 0)
        };
        label.SetDynamicResource(Label.TextColorProperty, "TextMuted");
        _content.Children.Add(label);
    }

    private void AddSignInHint()
    {
        if (_content.Children.OfType<Button>().Any(b => b.Text == "Sign in / set up household"))
            return;

        var hint = new Label
        {
            Text = "Sign in or finish setting up your household to use FamilyOS.",
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };
        hint.SetDynamicResource(Label.TextColorProperty, "TextMuted");
        var button = new Button { Text = "Sign in / set up household" };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        button.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(OnboardingPage));
        _content.Children.Add(hint);
        _content.Children.Add(button);
    }

    private static async Task NavigateForAsync(string kind)
    {
        var route = kind.Trim().ToLowerInvariant() switch
        {
            "request" => nameof(RequestsPage),
            "event" or "calendar" => "//calendar",
            "meal" or "recipe" => nameof(MealsPage),
            "message" or "chat" => nameof(ChatPage),
            "notification" => nameof(NotificationsPage),
            "family" or "member" or "shopping" or "procurement" => "//family",
            _ => "//tasks"
        };
        await Shell.Current.GoToAsync(route);
    }
}
