using FamilyOS.Maui.Services;
using Microsoft.Maui.Layouts;

namespace FamilyOS.Maui.Pages;

/// <summary>Household activity and progress metrics from the outcomes API.</summary>
public sealed class OutcomesPage : ContentPage
{
    private readonly OutcomesApiClient _api;
    private readonly VerticalStackLayout _content = new() { Spacing = 14 };
    private readonly Label _status = new() { FontSize = 13 };
    private readonly Button _refresh = new() { Text = "Refresh" };
    private bool _loading;

    public OutcomesPage(OutcomesApiClient api)
    {
        _api = api;
        Title = "Outcomes";
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();

        var heading = new Label { Text = "Family outcomes", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Children = { heading, _refresh }
        };
        Grid.SetColumn(_refresh, 1);
        var subtitle = new Label
        {
            Text = "A shared view of household progress and what still needs attention.",
            FontSize = 13
        };
        subtitle.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        var root = new VerticalStackLayout
        {
            Padding = new Thickness(18, 12),
            Spacing = 14,
            Children = { header, subtitle, _status, _content }
        };
        Content = new RefreshView
        {
            Content = new ScrollView { Content = root },
            Command = new Command(async () => await LoadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_content.Children.Count == 0 && !_loading) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _refresh.IsEnabled = false;
        _status.Text = "Loading household outcomes…";
        try
        {
            var data = await _api.GetAsync();
            _content.Children.Clear();

            AddSection("Current workload");
            AddMetricGrid(
                ("Open tasks", data.OpenTasks.ToString()),
                ("Pending requests", data.PendingRequests.ToString()),
                ("Shopping queue", data.QueuedShoppingItems.ToString()));

            AddSection("Last 7 days");
            AddMetricGrid(
                ("Tasks completed", data.TasksCompletedLast7Days.ToString()),
                ("Requests approved", data.RequestsApprovedLast7Days.ToString()),
                ("Requests denied", data.RequestsDeniedLast7Days.ToString()),
                ("Items purchased", data.ShoppingItemsPurchasedLast7Days.ToString()),
                ("Approval rate", data.ApprovalRateLast7Days is null ? "—" : $"{data.ApprovalRateLast7Days:0.#}%"),
                ("Average task time", data.AvgTaskCompletionMinutes is null ? "—" : $"{data.AvgTaskCompletionMinutes:0} min"));

            AddSection("Last 30 days");
            AddMetricGrid(
                ("Tasks completed", data.TasksCompletedLast30Days.ToString()),
                ("Requests approved", data.RequestsApprovedLast30Days.ToString()),
                ("Requests denied", data.RequestsDeniedLast30Days.ToString()));

            AddSection("Eight-week activity");
            AddWeeklySeries("Tasks completed", data.WeeklyTasksCompleted);
            AddWeeklySeries("Requests approved", data.WeeklyRequestsApproved);
            AddWeeklySeries("Shopping purchased", data.WeeklyShoppingPurchased);
            _status.Text = "Household metrics updated just now.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "The request timed out. Pull to refresh and try again.";
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            _status.Text = "FamilyOS could not be reached. Check your connection and try again.";
        }
        catch (Exception)
        {
            _status.Text = "Something went wrong while loading outcomes.";
        }
        finally
        {
            _loading = false;
            _refresh.IsEnabled = true;
        }
    }

    private void AddSection(string title)
    {
        var label = new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold };
        label.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        _content.Children.Add(label);
    }

    private void AddMetricGrid(params (string Label, string Value)[] metrics)
    {
        var grid = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start
        };
        foreach (var (label, value) in metrics)
        {
            var number = new Label { Text = value, FontSize = 24, FontAttributes = FontAttributes.Bold };
            number.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
            var caption = new Label { Text = label, FontSize = 12 };
            caption.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            var stack = new VerticalStackLayout { Spacing = 4, Children = { number, caption } };
            var card = new Border
            {
                Padding = 12,
                WidthRequest = 148,
                Margin = new Thickness(0, 0, 8, 8),
                Content = stack
            };
            card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
            grid.Children.Add(card);
        }
        _content.Children.Add(grid);
    }

    private void AddWeeklySeries(string title, IReadOnlyList<int> values)
    {
        var rows = new VerticalStackLayout { Spacing = 8 };
        var maximum = Math.Max(1, values.DefaultIfEmpty(0).Max());
        for (var i = 0; i < values.Count; i++)
        {
            var weekStart = DateTime.Today.AddDays(-7 * (7 - i));
            var label = new Label
            {
                Text = weekStart.ToString("MMM d"),
                FontSize = 12,
                WidthRequest = 54,
                VerticalTextAlignment = TextAlignment.Center
            };
            label.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            var bar = new ProgressBar
            {
                Progress = Math.Clamp(values[i] / (double)maximum, 0, 1),
                VerticalOptions = LayoutOptions.Center
            };
            bar.SetDynamicResource(ProgressBar.ProgressColorProperty, "AccentPrimary");
            var count = new Label
            {
                Text = values[i].ToString(),
                FontSize = 12,
                WidthRequest = 24,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center
            };
            count.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(58), new ColumnDefinition(GridLength.Star), new ColumnDefinition(28) },
                ColumnSpacing = 8,
                Children = { label, bar, count }
            };
            Grid.SetColumn(bar, 1);
            Grid.SetColumn(count, 2);
            rows.Children.Add(row);
        }
        var heading = new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var stack = new VerticalStackLayout { Spacing = 10, Children = { heading, rows } };
        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _content.Children.Add(card);
    }
}
