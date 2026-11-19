using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// API-backed view of the signed-in member's tasks. The API remains the
/// authority for task visibility, assignment, and allowed state transitions.
/// </summary>
public sealed class TasksPage : ContentPage
{
    private readonly TasksApiClient _tasks;
    private readonly VerticalStackLayout _items;
    private readonly Label _status;
    private readonly Button _refresh;
    private bool _loading;

    public TasksPage(TasksApiClient tasks)
    {
        _tasks = tasks;
        Title = "Tasks";

        _status = new Label { Text = "Loading your tasks…", FontSize = 14 };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _refresh = new Button { Text = "Refresh", HorizontalOptions = LayoutOptions.End };
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();

        _items = new VerticalStackLayout { Spacing = 12 };
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        var heading = new Label { Text = "My tasks", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        header.Add(heading, 0, 0);
        header.Add(_refresh, 1, 0);

        var layout = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12),
            Spacing = 16,
            Children = { header, _status, _items }
        };
        Content = new RefreshView
        {
            Content = new ScrollView { Content = layout },
            Command = new Command(async () => await LoadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_items.Children.Count == 0 && !_loading)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _refresh.IsEnabled = false;
        _status.Text = "Loading your tasks…";
        try
        {
            var tasks = await _tasks.GetMyTasksAsync();
            _items.Children.Clear();
            if (tasks.Count == 0)
            {
                AddCard("No assigned tasks", "Tasks assigned to you will appear here.");
                _status.Text = "You're all caught up.";
            }
            else
            {
                _status.Text = $"{tasks.Count} task{(tasks.Count == 1 ? "" : "s")} assigned to you";
                foreach (var task in tasks.OrderBy(t => t.IsOverdue ? 0 : 1).ThenBy(t => t.DueDate ?? DateTime.MaxValue))
                    AddTask(task);
            }
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
            _status.Text = "Something went wrong while loading your tasks.";
        }
        finally
        {
            _loading = false;
            _refresh.IsEnabled = true;
        }
    }

    private void AddTask(FamilyTaskDto task)
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        var title = new Label { Text = task.Title, FontSize = 17, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(title);

        var details = new List<string> { task.Status, task.Priority };
        if (!string.IsNullOrWhiteSpace(task.Category)) details.Add(task.Category);
        if (task.DueDate is not null) details.Add($"Due {task.DueDate.Value.ToLocalTime():MMM d}");
        if (task.IsOverdue) details.Add("Overdue");
        var meta = new Label { Text = string.Join(" · ", details), FontSize = 13 };
        meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        stack.Children.Add(meta);

        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            var description = new Label { Text = task.Description, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
            description.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            stack.Children.Add(description);
        }

        var actions = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row };
        foreach (var action in ActionsFor(task.Status))
        {
            var button = new Button { Text = action.Label, FontSize = 13, Margin = new Thickness(0, 0, 8, 0) };
            button.SetDynamicResource(VisualElement.StyleProperty,
                action.Primary ? "FoPrimaryButtonStyle" : "FoSecondaryButtonStyle");
            button.Clicked += async (_, _) => await ExecuteAsync(task.Id, action.Name);
            actions.Children.Add(button);
        }
        if (actions.Children.Count > 0) stack.Children.Add(actions);

        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _items.Children.Add(border);
    }

    private static IReadOnlyList<(string Name, string Label, bool Primary)> ActionsFor(string status)
        => status switch
        {
            "Assigned" or "Reassigned" => new[] { ("accept", "Accept", true) },
            "Accepted" => new[] { ("start", "Start", true), ("complete", "Complete", false) },
            "InProgress" => new[] { ("pause", "Pause", false), ("complete", "Complete", true) },
            "Paused" => new[] { ("resume", "Resume", true), ("complete", "Complete", false) },
            "Deferred" => new[] { ("start", "Start", true) },
            _ => Array.Empty<(string, string, bool)>()
        };

    private async Task ExecuteAsync(Guid taskId, string action)
    {
        _status.Text = "Saving task update…";
        _refresh.IsEnabled = false;
        try
        {
            await _tasks.RunActionAsync(taskId, action);
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "The task could not be updated. Refresh and try again.";
        }
        finally
        {
            _refresh.IsEnabled = true;
        }
    }

    private void AddCard(string title, string detail)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        var heading = new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var body = new Label { Text = detail, FontSize = 14 };
        body.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        stack.Children.Add(heading);
        stack.Children.Add(body);
        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _items.Children.Add(border);
    }
}
