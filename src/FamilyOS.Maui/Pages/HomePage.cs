using FamilyOS.Maui.Services;
using Microsoft.Maui.ApplicationModel;

namespace FamilyOS.Maui.Pages;

/// <summary>API-backed Family Pulse dashboard. All household data is server-authorized.</summary>
public sealed class HomePage : ContentPage
{
    private readonly PulseApiClient _pulseClient;
    private readonly VerticalStackLayout _content;
    private readonly Label _status;
    private readonly Button _refresh;
    private PulseDto? _snapshot;
    private bool _loading;

    public HomePage(PulseApiClient pulseClient)
    {
        _pulseClient = pulseClient;
        Title = "Pulse";

        _status = new Label { Text = "Loading your family dashboard…", FontSize = 14 };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        _refresh = new Button { Text = "Refresh", HorizontalOptions = LayoutOptions.End };
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();

        _content = new VerticalStackLayout { Padding = new Thickness(20, 12), Spacing = 16 };

        var brand = new HorizontalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Image
                {
                    Source = "logo_mark.png",
                    WidthRequest = 32,
                    HeightRequest = 32,
                    Aspect = Aspect.AspectFit,
                    VerticalOptions = LayoutOptions.Center
                },
                new Image
                {
                    Source = "logo_wordmark_on_dark.png",
                    HeightRequest = 24,
                    Aspect = Aspect.AspectFit,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        header.Add(brand, 0, 0);
        header.Add(_refresh, 1, 0);
        var heading = new Label
        {
            Text = "Family Pulse",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 8, 0, 0)
        }.WithStyle("TextDisplay");
        header.Add(heading, 0, 1);
        Grid.SetColumnSpan(heading, 2);

        var layout = new VerticalStackLayout { Spacing = 8, Children = { header, _status, _content } };
        Content = new RefreshView
        {
            Content = new ScrollView { Content = layout },
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
        _status.Text = pulse.RoleHint ?? "Your household at a glance.";
        AddText(pulse.Greeting, "TextTitle");
        AddText($"Your role: {pulse.Role}", "TextCaption");

        if (pulse.NextAction is not null)
        {
            var next = pulse.NextAction;
            var action = new Button { Text = $"{next.PrimaryAction}: {next.Title}", HorizontalOptions = LayoutOptions.Fill };
            action.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
            action.Clicked += async (_, _) => await NavigateForAsync(next.Kind);
            AddCard("Your next action", next.Subtitle, action, next.DueLabel);
        }

        AddSummary(pulse.Household);

        if (pulse.NeedsAttention.Count > 0)
            AddAttentionSection("Needs attention", pulse.NeedsAttention);

        if (pulse.WaitingBlockers.Count > 0)
            AddAttentionSection("Waiting and blockers", pulse.WaitingBlockers);

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
            AddCard("You’re all caught up", "There are no urgent actions or upcoming items in this snapshot.", null, "All clear");
        }
    }

    private void AddSummary(PulseHouseholdSummaryDto household)
    {
        AddSectionTitle("Household at a glance");
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
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
            var button = new Button { Text = item.ActionLabel, FontSize = 13, HorizontalOptions = LayoutOptions.End };
            button.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            button.Clicked += async (_, _) => await NavigateForAsync(item.Kind);
            AddCard(item.Title, item.Reason, button, item.Kind);
        }
    }

    private void AddCard(string title, string? subtitle, View? action, string? meta)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        var heading = new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(heading);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var detail = new Label { Text = subtitle, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
            detail.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            stack.Children.Add(detail);
        }
        if (!string.IsNullOrWhiteSpace(meta))
        {
            var caption = new Label { Text = meta, FontSize = 12 };
            caption.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            stack.Children.Add(caption);
        }
        if (action is not null) stack.Children.Add(action);

        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _content.Children.Add(border);
    }

    private View StatCard(int value, string label)
    {
        var valueLabel = new Label { Text = value.ToString(), FontSize = 24, FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center };
        valueLabel.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var caption = new Label { Text = label, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center };
        caption.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        var stack = new VerticalStackLayout { Spacing = 4, Children = { valueLabel, caption } };
        var border = new Border { Padding = 10, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return border;
    }

    private void AddSectionTitle(string title)
    {
        var label = new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold };
        label.SetDynamicResource(VisualElement.StyleProperty, "TextHeading");
        _content.Children.Add(label);
    }

    private void AddText(string value, string style)
    {
        var label = new Label { Text = value, FontSize = 15, LineBreakMode = LineBreakMode.WordWrap };
        label.SetDynamicResource(VisualElement.StyleProperty, style);
        _content.Children.Add(label);
    }

    private void AddSignInHint()
    {
        if (_content.Children.OfType<Button>().Any(b => b.Text == "Sign in / set up household"))
            return;

        var hint = new Label
        {
            Text = "Sign in or finish setting up your household to use FamilyOS.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        hint.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        var button = new Button { Text = "Sign in / set up household" };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        button.Clicked += async (_, _) =>
        {
            await Shell.Current.GoToAsync(nameof(OnboardingPage));
        };
        _content.Children.Add(hint);
        _content.Children.Add(button);
    }

    private static async Task NavigateForAsync(string kind)
    {
        var route = kind.Equals("Request", StringComparison.OrdinalIgnoreCase)
            ? nameof(RequestsPage)
            : "//tasks";
        await Shell.Current.GoToAsync(route);
    }
}

internal static class PulseViewExtensions
{
    public static T WithStyle<T>(this T view, string key) where T : VisualElement
    {
        view.SetDynamicResource(VisualElement.StyleProperty, key);
        return view;
    }
}
