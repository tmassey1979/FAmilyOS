using FamilyOS.Maui.Services;
using System.Globalization;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// API-backed view of the signed-in member's tasks.
/// Compact header aligned with the Home command-center look.
/// </summary>
public sealed partial class TasksPage : ContentPage, IQueryAttributable
{
    private readonly TasksApiClient _tasks;
    private readonly FamilyApiClient _family;
    private readonly PulseRefreshSignal _pulseRefresh;
    private readonly VerticalStackLayout _items;
    private readonly VerticalStackLayout _recurrences;
    private readonly Label _status;
    private readonly Button _refresh;
    private readonly Button _create;
    private readonly Button _scope;
    private readonly Label _heading;
    private bool _showFamilyTasks;
    private bool _loading;

    public TasksPage(TasksApiClient tasks, FamilyApiClient family, PulseRefreshSignal pulseRefresh)
    {
        _tasks = tasks;
        _family = family;
        _pulseRefresh = pulseRefresh;
        Title = "Tasks";

        _status = new Label { Text = "Loading your tasks\u2026", FontSize = 13 };
        _status.SetDynamicResource(Label.TextColorProperty, "TextSecondary");

        _refresh = new Button
        {
            Text = "Refresh",
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(12, 0),
            VerticalOptions = LayoutOptions.Center
        };
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();

        _create = new Button
        {
            Text = "New task",
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(14, 0),
            VerticalOptions = LayoutOptions.Center
        };
        _create.BackgroundColor = Color.FromArgb("#22D3EE");
        _create.TextColor = Color.FromArgb("#0A0E14");
        _create.CornerRadius = 18;
        _create.Clicked += async (_, _) => await CreateTaskAsync();

        _heading = new Label
        {
            Text = "My tasks",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        _heading.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

        _scope = new Button
        {
            Text = "Family",
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(12, 0),
            VerticalOptions = LayoutOptions.Center
        };
        _scope.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _scope.Clicked += async (_, _) =>
        {
            _showFamilyTasks = !_showFamilyTasks;
            _scope.Text = _showFamilyTasks ? "Mine" : "Family";
            _heading.Text = _showFamilyTasks ? "Household tasks" : "My tasks";
            await LoadAsync();
        };

        _items = new VerticalStackLayout { Spacing = 12 };
        _recurrences = new VerticalStackLayout { Spacing = 10 };

        var eyebrow = new Label
        {
            Text = "TASKS",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.6
        };
        eyebrow.SetDynamicResource(Label.TextColorProperty, "AccentGlow");

        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            VerticalOptions = LayoutOptions.Center
        };
        titleRow.Add(_heading, 0, 0);

        var actions = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            Children = { _scope, _create, _refresh }
        };
        titleRow.Add(actions, 1, 0);

        var scan = new BoxView { HeightRequest = 2, Opacity = 0.85, Margin = new Thickness(0, 2, 0, 0) };
        scan.SetDynamicResource(BoxView.ColorProperty, "AccentPrimary");

        var header = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { eyebrow, titleRow, scan, _status }
        };

        var routinesTitle = new Label
        {
            Text = "Recurring routines",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        };
        routinesTitle.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var routinesHint = new Label
        {
            Text = "Repeating household tasks \u2014 stop a series when it is no longer needed.",
            FontSize = 13
        };
        routinesHint.SetDynamicResource(Label.TextColorProperty, "TextSecondary");

        var layout = new VerticalStackLayout
        {
            Padding = new Thickness(16, 12),
            Spacing = 14,
            Children =
            {
                header,
                _items,
                routinesTitle,
                routinesHint,
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("scope", out var scope) &&
            string.Equals(scope?.ToString(), "family", StringComparison.OrdinalIgnoreCase))
        {
            _showFamilyTasks = true;
            _scope.Text = "Mine";
            _heading.Text = "Household tasks";
        }
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
        _refresh.IsEnabled = false;
        _create.IsEnabled = false;
        _status.Text = "Loading your tasks\u2026";
        try
        {
            var tasksTask = _showFamilyTasks ? _tasks.GetFamilyTasksAsync() : _tasks.GetMyTasksAsync();
            var recurrencesTask = _tasks.GetRecurrencesAsync();
            await Task.WhenAll(tasksTask, recurrencesTask);
            var tasks = await tasksTask;
            var recurrences = await recurrencesTask;
            _items.Children.Clear();
            _recurrences.Children.Clear();
            foreach (var recurrence in recurrences)
                AddRecurrence(recurrence);
            if (recurrences.Count == 0)
                AddEmptyCard(_recurrences, "No recurring routines", "Add a daily, weekly, or monthly household routine.");
            if (tasks.Count == 0)
            {
                AddEmptyCard(_items, _showFamilyTasks ? "No household tasks" : "No assigned tasks",
                    _showFamilyTasks ? "Create a task and assign it to a household member." : "Tasks assigned to you will appear here.");
                _status.Text = "You're all caught up.";
            }
            else
            {
                var overdue = tasks.Count(t => t.IsOverdue);
                _status.Text = overdue > 0
                    ? $"{tasks.Count} task{(tasks.Count == 1 ? "" : "s")} \u00B7 {overdue} overdue"
                    : _showFamilyTasks
                        ? $"{tasks.Count} household task{(tasks.Count == 1 ? "" : "s")}"
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

    private void AddEmptyCard(VerticalStackLayout host, string title, string body)
    {
        var t = new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        t.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var b = new Label { Text = body, FontSize = 13 };
        b.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var card = new Border
        {
            Padding = new Thickness(16),
            StrokeThickness = 1,
            Content = new VerticalStackLayout { Spacing = 6, Children = { t, b } }
        };
        card.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        card.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        card.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 };
        host.Children.Add(card);
    }

    private void AddTask(FamilyTaskDto task)
    {
        var content = new VerticalStackLayout { Spacing = 10 };

        var title = new Label
        {
            Text = task.Title,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

        var status = CreateTaskChip(FormatTaskStatus(task.Status), "BgGlow", "AccentPrimary");
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        header.Add(title, 0, 0);
        header.Add(status, 1, 0);
        content.Children.Add(header);

        var tags = new HorizontalStackLayout { Spacing = 6 };
        tags.Children.Add(CreateTaskChip(task.Priority, "BgSurfaceAlt", "TextPrimary"));
        if (!string.IsNullOrWhiteSpace(task.Category))
            tags.Children.Add(CreateTaskChip(task.Category!, "BgSurfaceAlt", "TextPrimary"));
        if (task.IsOverdue)
            tags.Children.Add(CreateTaskChip("OVERDUE", "BgGlow", "AccentDanger"));
        content.Children.Add(tags);

        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            var description = new Label
            {
                Text = task.Description,
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap,
                MaxLines = 3
            };
            description.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
            content.Children.Add(description);
        }

        if (task.DueDate is { } due)
        {
            var dueLabel = new Label
            {
                Text = $"Due {due.ToLocalTime().ToString("ddd, MMM d \u00B7 h:mm tt", CultureInfo.CurrentCulture)}",
                FontSize = 12
            };
            dueLabel.SetDynamicResource(Label.TextColorProperty, "TextMuted");
            content.Children.Add(dueLabel);
        }

        var actions = new HorizontalStackLayout { Spacing = 8 };
        foreach (var (action, label, primary) in ActionsFor(task.Status).Take(2))
            actions.Children.Add(MakeActionButton(task, action, label, primary));
        actions.Children.Add(MakeMoreButton(task));
        content.Children.Add(actions);

        var card = new Border
        {
            Padding = new Thickness(16, 14),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Content = content
        };
        card.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        card.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        _items.Children.Add(card);
    }

    private void AddRecurrence(RecurrenceDto recurrence)
    {
        var title = new Label { Text = recurrence.Title, FontSize = 15, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var meta = new Label { Text = recurrence.Frequency ?? "Recurring", FontSize = 12 };
        meta.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var card = new Border
        {
            Padding = new Thickness(14),
            StrokeThickness = 1,
            Content = new VerticalStackLayout { Spacing = 4, Children = { title, meta } }
        };
        card.SetDynamicResource(Border.BackgroundColorProperty, "BgSurface");
        card.SetDynamicResource(Border.StrokeProperty, "BorderSubtle");
        card.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 };
        _recurrences.Children.Add(card);
    }

    private static string FormatTaskStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "UNKNOWN";
        var spaced = System.Text.RegularExpressions.Regex.Replace(status, "([a-z])([A-Z])", "$1 $2");
        return spaced.ToUpperInvariant();
    }

    private static Border CreateTaskChip(string text, string backgroundResource, string foregroundResource)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        label.SetDynamicResource(Label.TextColorProperty, foregroundResource);
        var chip = new Border
        {
            Padding = new Thickness(9, 5),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Content = label,
            Margin = new Thickness(0, 0, 4, 0)
        };
        chip.SetDynamicResource(VisualElement.BackgroundColorProperty, backgroundResource);
        return chip;
    }

    private static string NormalizeTaskStatus(string? status) =>
        System.Text.RegularExpressions.Regex.Replace(status?.Trim() ?? string.Empty, @"[\s_-]+", string.Empty);

    private static (string action, string label, bool primary)[] ActionsFor(string status) =>
        NormalizeTaskStatus(status) switch
        {
            // Older/unassigned tasks stay in Created until an adult assigns them.
            // Make that first step visible instead of hiding it in the More menu.
            "Created" => new[] { ("assign", "Assign Task", true) },
            "Assigned" or "Reassigned" => new[] { ("accept", "Accept Task", true), ("decline", "Decline", false) },
            "Accepted" => new[] { ("start", "Start Task", true), ("complete", "Complete", false) },
            "InProgress" => new[] { ("complete", "Complete Task", true), ("pause", "Pause", false) },
            "Paused" => new[] { ("resume", "Resume Task", true) },
            "Deferred" => new[] { ("start", "Start Task", true) },
            _ => Array.Empty<(string, string, bool)>()
        };

    private Button MakeActionButton(FamilyTaskDto task, string action, string label, bool primary)
    {
        var button = new Button
        {
            Text = label,
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(14, 0),
            CornerRadius = 18
        };
        if (primary)
        {
            button.BackgroundColor = Color.FromArgb("#22D3EE");
            button.TextColor = Color.FromArgb("#0A0E14");
        }
        else
        {
            button.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        }
        button.Clicked += async (_, _) =>
        {
            if (action == "assign")
                await AssignTaskAsync(task);
            else
                await ExecuteAsync(task, action);
        };
        return button;
    }

    private Button MakeMoreButton(FamilyTaskDto task)
    {
        var button = new Button
        {
            Text = "More",
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        button.Clicked += async (_, _) =>
        {
            var pick = await DisplayActionSheet("Task actions", "Close", null, "History", "Cancel task");
            if (pick == "History") await ShowHistoryAsync(task.Id, task.Title);
            else if (pick == "Cancel task") await ExecuteAsync(task, "cancel");
        };
        return button;
    }

    private async Task AssignTaskAsync(FamilyTaskDto task)
    {
        try
        {
            var family = await _family.GetFamilyAsync();
            var members = family.Members
                .Where(member => member.IsActive)
                .OrderBy(member => member.DisplayName)
                .ToList();

            if (members.Count == 0)
            {
                await DisplayAlert("Assign task", "There are no active household members to assign this task to.", "OK");
                return;
            }

            var choices = members.Select(member => member.DisplayName).ToArray();
            var selected = await DisplayActionSheet("Assign task to", "Cancel", null, choices);
            var memberIndex = Array.IndexOf(choices, selected);
            if (memberIndex < 0)
                return;

            DateTime? dueDate = task.DueDate;
            if (!dueDate.HasValue)
            {
                var enteredDate = await DisplayPromptAsync(
                    "Due date required",
                    "This older task has no due date. Enter one as YYYY-MM-DD to assign it:",
                    "Save date",
                    "Cancel",
                    "YYYY-MM-DD",
                    initialValue: DateTime.Today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                if (string.IsNullOrWhiteSpace(enteredDate))
                    return;

                if (!DateTime.TryParseExact(
                    enteredDate.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
                {
                    await DisplayAlert("Invalid date", "Enter the date in YYYY-MM-DD format.", "OK");
                    return;
                }

                dueDate = DateTime.SpecifyKind(parsedDate.Date.AddHours(17), DateTimeKind.Local).ToUniversalTime();
                await _tasks.UpdateDetailsAsync(task.Id, new UpdateTaskDetailsBody(DueDate: dueDate));
            }

            var assignee = members[memberIndex];
            await _tasks.AssignAsync(task.Id, assignee.Id);
            _status.Text = $"Task assigned to {assignee.DisplayName}.";
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Assignment failed", ex.Message, "OK");
        }
    }

    private async Task ExecuteAsync(FamilyTaskDto task, string action)
    {
        try
        {
            switch (action)
            {
                case "accept": await _tasks.RunActionAsync(task.Id, "accept"); break;
                case "decline": await _tasks.DeclineAsync(task.Id); break;
                case "start": await _tasks.RunActionAsync(task.Id, "start"); break;
                case "complete": await _tasks.RunActionAsync(task.Id, "complete"); break;
                case "pause": await _tasks.RunActionAsync(task.Id, "pause"); break;
                case "resume": await _tasks.RunActionAsync(task.Id, "resume"); break;
                case "cancel": await _tasks.CancelAsync(task.Id); break;
                case "defer": await _tasks.DeferAsync(task.Id); break;
                case "skip": await _tasks.SkipAsync(task.Id); break;
            }
            _pulseRefresh.RequestRefresh();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Task action failed", ex.Message, "OK");
        }
    }

    private async Task ShowHistoryAsync(Guid taskId, string title)
    {
        try
        {
            var history = await _tasks.GetHistoryAsync(taskId);
            var lines = history.Select(h => $"{h.AtLocal:g} \u00B7 {h.Summary}").ToArray();
            await DisplayAlert(title, lines.Length == 0 ? "No history yet." : string.Join("\n", lines.Take(12)), "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("History", ex.Message, "OK");
        }
    }
}
