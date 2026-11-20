using Microsoft.Maui.Layouts;
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
    private readonly VerticalStackLayout _recurrences;
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
        _recurrences = new VerticalStackLayout { Spacing = 10 };
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
            Children = { header, _status, _items,
                new Label { Text = "Recurring routines", FontSize = 20, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Create repeating household tasks and stop a series when it is no longer needed.", FontSize = 13 },
                CreateRecurrenceButton(), _recurrences }
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
            var tasksTask = _tasks.GetMyTasksAsync();
            var recurrencesTask = _tasks.GetRecurrencesAsync();
            await Task.WhenAll(tasksTask, recurrencesTask);
            var tasks = await tasksTask;
            var recurrences = await recurrencesTask;
            _items.Children.Clear();
            _recurrences.Children.Clear();
            foreach (var recurrence in recurrences)
                AddRecurrence(recurrence);
            if (recurrences.Count == 0)
                AddCard(_recurrences, "No recurring routines", "Add a daily, weekly, biweekly, monthly, or yearly household routine.");
            if (tasks.Count == 0)
            {
                AddCard(_items, "No assigned tasks", "Tasks assigned to you will appear here.");
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

    private Button CreateRecurrenceButton()
    {
        var button = new Button { Text = "＋ Add recurring routine" };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        button.Clicked += async (_, _) => await CreateRecurrenceAsync();
        return button;
    }

    private void AddRecurrence(RecurrenceDto recurrence)
    {
        var frequency = recurrence.Frequency switch
        {
            "Daily" => recurrence.Interval == 1 ? "Daily" : $"Every {recurrence.Interval} days",
            "Weekly" => recurrence.Interval == 1 ? "Weekly" : $"Every {recurrence.Interval} weeks",
            "BiWeekly" => "Every two weeks",
            "Monthly" => recurrence.Interval == 1 ? "Monthly" : $"Every {recurrence.Interval} months",
            "Yearly" => recurrence.Interval == 1 ? "Yearly" : $"Every {recurrence.Interval} years",
            _ => "Repeating"
        };
        var next = recurrence.NextOccurrenceUtc is DateTime when
            ? $"Next: {when.ToLocalTime():ddd, MMM d}" : "Next occurrence not scheduled";
        var detail = $"{frequency} · {next}{(string.IsNullOrWhiteSpace(recurrence.Category) ? "" : $" · {recurrence.Category}")}";
        var stack = new VerticalStackLayout { Spacing = 5 };
        var title = new Label { Text = recurrence.Title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var meta = new Label { Text = recurrence.IsActive ? detail : $"{detail} · Inactive", FontSize = 12 };
        meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        stack.Children.Add(title);
        stack.Children.Add(meta);
        if (recurrence.IsActive)
        {
            var pause = new Button { Text = "Stop series", FontSize = 12 };
            pause.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            pause.Clicked += async (_, _) => await DeactivateRecurrenceAsync(recurrence);
            stack.Children.Add(pause);
        }
        var card = new Border { Padding = 12, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _recurrences.Children.Add(card);
    }

    private async Task CreateRecurrenceAsync()
    {
        var title = await DisplayPromptAsync("New recurring routine", "What needs to repeat?", maxLength: 160);
        if (string.IsNullOrWhiteSpace(title)) return;
        var choice = await DisplayActionSheetAsync("Repeat", "Cancel", null,
            "Daily", "Weekly", "Every two weeks", "Monthly", "Yearly");
        if (choice is null or "Cancel") return;
        var frequency = choice switch
        {
            "Daily" => "Daily", "Weekly" => "Weekly", "Every two weeks" => "BiWeekly",
            "Monthly" => "Monthly", "Yearly" => "Yearly", _ => ""
        };
        if (string.IsNullOrEmpty(frequency)) return;
        var interval = 1;
        if (frequency is "Daily" or "Weekly" or "Monthly" or "Yearly")
        {
            var intervalText = await DisplayPromptAsync("Repeat interval", "Enter 1 for every period, 2 for every second period, etc.",
                initialValue: "1", maxLength: 3, keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(intervalText)) return;
            if (!int.TryParse(intervalText, out interval) || interval is < 1 or > 365)
            {
                await DisplayAlertAsync("Invalid interval", "Enter a whole number from 1 to 365.", "OK");
                return;
            }
        }
        int? dayOfWeek = null;
        if (frequency is "Weekly" or "BiWeekly")
        {
            var day = await DisplayActionSheetAsync("Day of week", "Use today", null,
                "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday");
            if (day is not null and not "Cancel" and not "Use today")
                dayOfWeek = Array.IndexOf(new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }, day);
            else if (day == "Use today")
                dayOfWeek = (int)DateTime.Today.DayOfWeek;
        }
        var description = await DisplayPromptAsync("Description", "Optional details", maxLength: 500);
        var category = await DisplayPromptAsync("Category", "Optional category", maxLength: 80);
        try
        {
            await _tasks.CreateRecurrenceAsync(new CreateRecurrenceBody(
                title.Trim(), frequency, interval, dayOfWeek is null ? null : ((DayOfWeek)dayOfWeek.Value).ToString(),
                Category: string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                Description: string.IsNullOrWhiteSpace(description) ? null : description.Trim()));
            _status.Text = "Recurring routine created.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Routine not created",
                ex is FamilyApiException ? ex.Message : "Could not create the recurring routine.", "OK");
        }
    }

    private async Task DeactivateRecurrenceAsync(RecurrenceDto recurrence)
    {
        if (!await DisplayAlertAsync("Stop recurring routine",
                $"Stop creating future tasks for “{recurrence.Title}”? Existing tasks will remain.", "Stop series", "Keep active"))
            return;
        try
        {
            await _tasks.DeactivateRecurrenceAsync(recurrence.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Routine not paused",
                ex is FamilyApiException ? ex.Message : "Could not pause this routine.", "OK");
        }
    }

    private void AddCard(VerticalStackLayout target, string title, string detail)
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
        target.Children.Add(border);
    }
}
