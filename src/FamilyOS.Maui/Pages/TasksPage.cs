using FamilyOS.Maui.Services;
using System.Globalization;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// API-backed view of the signed-in member's tasks. The API remains the
/// authority for task visibility, assignment, and allowed state transitions.
/// Assignment is distinct from acceptance: "Assigned" requires Accept before Start.
/// </summary>
public sealed class TasksPage : ContentPage
{
    private readonly TasksApiClient _tasks;
    private readonly FamilyApiClient _family;
    private readonly VerticalStackLayout _items;
    private readonly VerticalStackLayout _recurrences;
    private readonly Label _status;
    private readonly Button _refresh;
    private readonly Button _create;
    private bool _loading;

    public TasksPage(TasksApiClient tasks, FamilyApiClient family)
    {
        _tasks = tasks;
        _family = family;
        Title = "Tasks";

        _status = new Label { Text = "Loading your tasks…", FontSize = 14 };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _refresh = new Button { Text = "Refresh" };
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();
        _create = new Button { Text = "New task" };
        _create.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _create.Clicked += async (_, _) => await CreateTaskAsync();

        _items = new VerticalStackLayout { Spacing = 12 };
        _recurrences = new VerticalStackLayout { Spacing = 10 };
        var heading = new Label { Text = "My tasks", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var toolbar = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Children = { _create, _refresh }
        };
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Children = { heading, toolbar }
        };
        Grid.SetColumn(toolbar, 1);

        var layout = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12),
            Spacing = 16,
            Children =
            {
                header, _status, _items,
                new Label { Text = "Recurring routines", FontSize = 20, FontAttributes = FontAttributes.Bold },
                new Label
                {
                    Text = "Create repeating household tasks and stop a series when it is no longer needed.",
                    FontSize = 13
                },
                CreateRecurrenceButton(),
                _recurrences
            }
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
        _create.IsEnabled = false;
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
                AddCard(_items, "No assigned tasks", "Tasks assigned to you will appear here. Use New task to create one.");
                _status.Text = "You're all caught up.";
            }
            else
            {
                var overdue = tasks.Count(t => t.IsOverdue);
                _status.Text = overdue > 0
                    ? $"{tasks.Count} task{(tasks.Count == 1 ? "" : "s")} · {overdue} overdue"
                    : $"{tasks.Count} task{(tasks.Count == 1 ? "" : "s")} assigned to you";
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
            _create.IsEnabled = true;
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
        if (task.IsOverdue) details.Add("Overdue");
        if (task.IsRecurring) details.Add(task.RecurrenceFrequency is { Length: > 0 } f ? $"↻ {f}" : "↻ Recurring");
        if (task.DueDate is { } due) details.Add($"Due {due.ToLocalTime():g}");
        if (task.EstimatedDurationMinutes is { } est) details.Add($"Est {est} min");
        if (task.ActualDurationMinutes is { } act) details.Add($"Actual {act} min");
        if (!string.IsNullOrWhiteSpace(task.PlannedStartTime) || !string.IsNullOrWhiteSpace(task.PlannedEndTime))
            details.Add($"Planned {task.PlannedStartTime ?? "?"}–{task.PlannedEndTime ?? "?"}");
        if (task.VarianceMinutes is { } variance)
            details.Add(variance == 0 ? "On plan" : variance > 0 ? $"+{variance} min vs plan" : $"{variance} min vs plan");

        var meta = new Label { Text = string.Join(" · ", details), FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
        meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        stack.Children.Add(meta);

        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            var desc = new Label { Text = task.Description, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
            desc.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            stack.Children.Add(desc);
        }

        // Assignment ≠ acceptance: Assigned/Reassigned must Accept before Start.
        var actions = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
        };
        foreach (var (action, label, primary) in ActionsFor(task.Status))
            actions.Children.Add(MakeActionButton(task.Id, action, label, primary));

        actions.Children.Add(MakeDetailsButton(task));
        actions.Children.Add(MakeAssignButton(task));
        actions.Children.Add(MakeHistoryButton(task.Id, task.Title));
        stack.Children.Add(actions);

        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _items.Children.Add(border);
    }

    private static (string action, string label, bool primary)[] ActionsFor(string status) => status switch
    {
        "Assigned" or "Reassigned" => new[]
        {
            ("accept", "Accept", true),
            ("defer", "Defer", false),
            ("cancel", "Cancel", false)
        },
        "Accepted" => new[]
        {
            ("start", "Start", true),
            ("complete", "Complete", false),
            ("defer", "Defer", false),
            ("skip", "Skip", false),
            ("cancel", "Cancel", false)
        },
        "InProgress" => new[]
        {
            ("pause", "Pause", false),
            ("complete", "Complete", true),
            ("defer", "Defer", false),
            ("cancel", "Cancel", false)
        },
        "Paused" => new[]
        {
            ("resume", "Resume", true),
            ("complete", "Complete", false),
            ("cancel", "Cancel", false)
        },
        "Deferred" => new[]
        {
            ("start", "Start", true),
            ("skip", "Skip", false),
            ("cancel", "Cancel", false)
        },
        _ => Array.Empty<(string, string, bool)>()
    };

    private Button MakeActionButton(Guid taskId, string action, string label, bool primary)
    {
        var button = new Button { Text = label, FontSize = 12, Margin = new Thickness(0, 4, 8, 0) };
        button.SetDynamicResource(VisualElement.StyleProperty, primary ? "FoPrimaryButtonStyle" : "FoSecondaryButtonStyle");
        button.Clicked += async (_, _) => await ExecuteAsync(taskId, action);
        return button;
    }

    private Button MakeDetailsButton(FamilyTaskDto task)
    {
        var button = new Button { Text = "Edit details", FontSize = 12, Margin = new Thickness(0, 4, 8, 0) };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        button.Clicked += async (_, _) => await EditTaskAsync(task);
        return button;
    }

    private Button MakeAssignButton(FamilyTaskDto task)
    {
        var button = new Button { Text = "Assign", FontSize = 12, Margin = new Thickness(0, 4, 8, 0) };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        button.Clicked += async (_, _) => await ReassignTaskAsync(task);
        return button;
    }

    private async Task EditTaskAsync(FamilyTaskDto task)
    {
        var title = await DisplayPromptAsync("Edit task", "Task title", initialValue: task.Title, maxLength: 120);
        if (string.IsNullOrWhiteSpace(title)) return;
        var description = await DisplayPromptAsync("Description", "Task details (optional)", initialValue: task.Description ?? "", maxLength: 1000);
        var category = await DisplayPromptAsync("Category", "Category (optional)", initialValue: task.Category ?? "", maxLength: 60);
        var dueText = await DisplayPromptAsync("Due date", "YYYY-MM-DD, or leave blank to clear the due date",
            initialValue: task.DueDate?.ToLocalTime().ToString("yyyy-MM-dd") ?? "", maxLength: 10);
        DateTime? dueDate = null;
        var clearDueDate = string.IsNullOrWhiteSpace(dueText);
        if (!clearDueDate)
        {
            if (!DateTime.TryParseExact(dueText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedDate))
            {
                await DisplayAlertAsync("Invalid date", "Enter the due date as YYYY-MM-DD.", "OK");
                return;
            }
            dueDate = DateTime.SpecifyKind(parsedDate.Date.AddHours(23).AddMinutes(59), DateTimeKind.Local).ToUniversalTime();
        }

        try
        {
            await _tasks.UpdateDetailsAsync(task.Id, new UpdateTaskDetailsBody(
                Title: title.Trim(),
                Description: string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Category: string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                DueDate: dueDate,
                ClearDueDate: clearDueDate));
            _status.Text = "Task details updated.";
            await LoadAsync();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not update this task."; }
    }

    private async Task ReassignTaskAsync(FamilyTaskDto task)
    {
        try
        {
            var family = await _family.GetFamilyAsync();
            var members = family.Members.Where(m => m.IsActive).OrderBy(m => m.DisplayName).ToList();
            if (members.Count == 0)
            {
                await DisplayAlertAsync("Assign task", "There are no active household members to assign this task to.", "OK");
                return;
            }

            var choices = members.Select(m => m.DisplayName).ToArray();
            var selected = await DisplayActionSheetAsync("Assign task to", "Cancel", null, choices);
            if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel") return;
            var member = members.FirstOrDefault(m => m.DisplayName == selected);
            if (member is null) return;

            await _tasks.ReassignAsync(task.Id, member.Id);
            _status.Text = $"Task assigned to {member.DisplayName}.";
            await LoadAsync();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not reassign this task."; }
    }

    private Button MakeHistoryButton(Guid taskId, string title)
    {
        var button = new Button { Text = "History", FontSize = 12, Margin = new Thickness(0, 4, 8, 0) };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        button.Clicked += async (_, _) => await ShowHistoryAsync(taskId, title);
        return button;
    }

    private async Task ExecuteAsync(Guid taskId, string action)
    {
        try
        {
            if (action is "defer" or "skip" or "cancel")
            {
                var prompt = action switch
                {
                    "defer" => "Optional note for deferring this task:",
                    "skip" => "Optional note for skipping this instance:",
                    _ => "Optional reason for cancelling:"
                };
                var note = await DisplayPromptAsync(action[..1].ToUpper() + action[1..], prompt, "Confirm", "Cancel", maxLength: 200);
                // null = user cancelled dialog
                if (note is null && action == "cancel")
                    return;
                if (note is null && action is "defer" or "skip")
                {
                    // Allow empty note via blank confirm is DisplayPrompt returns "" on empty — null means cancel.
                    return;
                }

                _ = action switch
                {
                    "defer" => await _tasks.DeferAsync(taskId, string.IsNullOrWhiteSpace(note) ? null : note),
                    "skip" => await _tasks.SkipAsync(taskId, string.IsNullOrWhiteSpace(note) ? null : note),
                    "cancel" => await _tasks.CancelAsync(taskId, string.IsNullOrWhiteSpace(note) ? null : note),
                    _ => throw new InvalidOperationException()
                };
            }
            else
            {
                await _tasks.RunActionAsync(taskId, action);
            }

            _status.Text = $"Task {action}ed.";
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = $"Could not {action} that task.";
        }
    }

    private async Task ShowHistoryAsync(Guid taskId, string title)
    {
        try
        {
            var history = await _tasks.GetHistoryAsync(taskId);
            if (history.Count == 0)
            {
                await DisplayAlertAsync(title, "No history recorded yet.", "OK");
                return;
            }

            var lines = history
                .OrderByDescending(h => h.OccurredAtUtc)
                .Take(20)
                .Select(h =>
                {
                    var who = string.IsNullOrWhiteSpace(h.ActorName) ? "" : $" · {h.ActorName}";
                    var detail = string.IsNullOrWhiteSpace(h.Detail) ? "" : $" — {h.Detail}";
                    return $"{h.OccurredAtUtc.ToLocalTime():g} · {h.Action}{who}{detail}";
                });
            await DisplayAlertAsync($"History · {title}", string.Join("\n", lines), "OK");
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not load task history.";
        }
    }

    private async Task CreateTaskAsync()
    {
        var title = await DisplayPromptAsync("New task", "What needs to get done?", "Next", "Cancel", maxLength: 120);
        if (string.IsNullOrWhiteSpace(title)) return;

        var description = await DisplayPromptAsync("Task details", "Add instructions or context (optional):", "Next", "Skip", maxLength: 1000);
        var priority = await DisplayActionSheetAsync("Priority", "Cancel", null, "Low", "Normal", "High", "Urgent");
        if (priority is null or "Cancel") priority = "Normal";

        var category = await DisplayPromptAsync("Category", "Optional category (for example: Chores, School, Home):", "Next", "Skip", maxLength: 60);
        var dueText = await DisplayPromptAsync("Due date", "Optional date in YYYY-MM-DD format:", "Next", "Skip", maxLength: 10);
        DateTime? dueDate = null;
        if (!string.IsNullOrWhiteSpace(dueText))
        {
            if (!DateTime.TryParseExact(dueText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedDate))
            {
                await DisplayAlertAsync("Invalid date", "Enter the due date as YYYY-MM-DD, or leave it blank.", "OK");
                return;
            }

            dueDate = DateTime.SpecifyKind(parsedDate.Date.AddHours(23).AddMinutes(59), DateTimeKind.Local).ToUniversalTime();
        }

        var estimateText = await DisplayPromptAsync("Time estimate", "Estimated minutes (optional):", "Create", "Skip", maxLength: 5, keyboard: Keyboard.Numeric);
        int? estimate = null;
        if (!string.IsNullOrWhiteSpace(estimateText))
        {
            if (!int.TryParse(estimateText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes is < 1 or > 10080)
            {
                await DisplayAlertAsync("Invalid estimate", "Enter a whole number from 1 to 10080 minutes, or leave it blank.", "OK");
                return;
            }

            estimate = minutes;
        }

        Guid? assigneeId = null;
        try
        {
            var family = await _family.GetFamilyAsync();
            var members = family.Members.Where(m => m.IsActive).OrderBy(m => m.DisplayName).ToList();
            if (members.Count > 0)
            {
                var choices = new[] { "Leave unassigned" }.Concat(members.Select(m => m.DisplayName)).ToArray();
                var selected = await DisplayActionSheetAsync("Assign to", "Cancel", null, choices);
                if (selected == "Cancel") return;
                if (!string.IsNullOrWhiteSpace(selected) && selected != "Leave unassigned")
                    assigneeId = members.FirstOrDefault(m => m.DisplayName == selected)?.Id;
            }

            await _tasks.CreateTaskAsync(new CreateTaskBody(
                title.Trim(),
                Description: string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Priority: priority,
                Category: string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                DueDate: dueDate,
                EstimatedDurationMinutes: estimate,
                AssignToMemberId: assigneeId));
            _status.Text = "Task created.";
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not create the task.";
        }
    }

    private Button CreateRecurrenceButton()
    {
        var button = new Button { Text = "Add recurring routine" };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        button.Clicked += async (_, _) => await CreateRecurrenceAsync();
        return button;
    }

    private void AddRecurrence(RecurrenceDto recurrence)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        var title = new Label { Text = recurrence.Title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(title);

        var bits = new List<string>
        {
            recurrence.IsActive ? "Active" : "Stopped",
            $"{recurrence.Frequency} / {recurrence.Interval}"
        };
        if (!string.IsNullOrWhiteSpace(recurrence.DayOfWeek)) bits.Add(recurrence.DayOfWeek);
        if (!string.IsNullOrWhiteSpace(recurrence.Category)) bits.Add(recurrence.Category!);
        if (recurrence.NextOccurrenceUtc is { } next) bits.Add($"Next {next.ToLocalTime():g}");

        var meta = new Label { Text = string.Join(" · ", bits), FontSize = 12 };
        meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        stack.Children.Add(meta);

        if (recurrence.IsActive)
        {
            var stop = new Button { Text = "Stop series", FontSize = 12 };
            stop.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
            stop.Clicked += async (_, _) => await DeactivateRecurrenceAsync(recurrence);
            stack.Children.Add(stop);
        }

        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _recurrences.Children.Add(border);
    }

    private async Task CreateRecurrenceAsync()
    {
        var title = await DisplayPromptAsync("Recurring routine", "Title", "Next", "Cancel", maxLength: 120);
        if (string.IsNullOrWhiteSpace(title)) return;

        var choice = await DisplayActionSheetAsync("Repeat", "Cancel", null,
            "Daily", "Weekly", "Biweekly", "Monthly", "Yearly");
        if (choice is null or "Cancel") return;

        string frequency = choice switch
        {
            "Daily" => "Daily",
            "Weekly" => "Weekly",
            "Biweekly" => "Weekly",
            "Monthly" => "Monthly",
            "Yearly" => "Yearly",
            _ => "Weekly"
        };
        var interval = choice == "Biweekly" ? 2 : 1;
        string? day = null;
        if (frequency == "Weekly")
        {
            day = await DisplayActionSheetAsync("Day of week", "Use today", null,
                "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday");
            if (day is not null and not "Cancel" and not "Use today")
            { /* keep */ }
            else
                day = DateTime.Today.DayOfWeek.ToString();
        }

        try
        {
            await _tasks.CreateRecurrenceAsync(new CreateRecurrenceBody(
                title.Trim(), frequency, interval, day));
            _status.Text = "Recurring routine created.";
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not create the recurring routine.";
        }
    }

    private async Task DeactivateRecurrenceAsync(RecurrenceDto recurrence)
    {
        var ok = await DisplayAlertAsync("Stop series", $"Stop “{recurrence.Title}”? Existing tasks stay; no new instances will be generated.", "Stop", "Keep");
        if (!ok) return;
        try
        {
            await _tasks.DeactivateRecurrenceAsync(recurrence.Id);
            _status.Text = "Recurring series stopped.";
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not stop that series.";
        }
    }

    private static void AddCard(VerticalStackLayout host, string title, string body)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold },
                new Label { Text = body, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap }
            }
        };
        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        host.Children.Add(border);
    }
}
