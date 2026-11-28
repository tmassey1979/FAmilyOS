using FamilyOS.Maui.Services;
using System.Globalization;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// API-backed view of the signed-in member's tasks. The API remains the
/// authority for task visibility, assignment, and allowed state transitions.
/// Assignment is distinct from acceptance: "Assigned" requires Accept before Start.
/// </summary>
public sealed class TasksPage : ContentPage, IQueryAttributable
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

        _status = new Label { Text = "Loading your tasks…", FontSize = 14 };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _refresh = new Button { Text = "Refresh" };
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();
        _create = new Button { Text = "New task" };
        _create.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _create.Clicked += async (_, _) => await CreateTaskAsync();
        _heading = new Label { Text = "My tasks", FontSize = 26, FontAttributes = FontAttributes.Bold };
        _heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        _scope = new Button { Text = "Family tasks", FontSize = 12 };
        _scope.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _scope.Clicked += async (_, _) =>
        {
            _showFamilyTasks = !_showFamilyTasks;
            _scope.Text = _showFamilyTasks ? "My tasks" : "Family tasks";
            _heading.Text = _showFamilyTasks ? "Household tasks" : "My tasks";
            await LoadAsync();
        };

        _items = new VerticalStackLayout { Spacing = 12 };
        _recurrences = new VerticalStackLayout { Spacing = 10 };
        var toolbar = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Children = { _scope, _create, _refresh }
        };
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Children = { _heading, toolbar }
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("scope", out var scope) &&
            string.Equals(scope?.ToString(), "family", StringComparison.OrdinalIgnoreCase))
        {
            _showFamilyTasks = true;
            _scope.Text = "My tasks";
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
        _status.Text = "Loading your tasks…";
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
                AddCard(_recurrences, "No recurring routines", "Add a daily, weekly, biweekly, monthly, or yearly household routine.");
            if (tasks.Count == 0)
            {
                AddCard(_items, _showFamilyTasks ? "No household tasks" : "No assigned tasks", _showFamilyTasks ? "Create a task and assign it to a household member." : "Tasks assigned to you will appear here. Use New task to create one.");
                _status.Text = "You're all caught up.";
            }
            else
            {
                var overdue = tasks.Count(t => t.IsOverdue);
                _status.Text = overdue > 0
                    ? $"{tasks.Count} task{(tasks.Count == 1 ? "" : "s")} · {overdue} overdue"
                    : _showFamilyTasks ? $"{tasks.Count} household task{(tasks.Count == 1 ? "" : "s")}" : $"{tasks.Count} task{(tasks.Count == 1 ? "" : "s")} assigned to you";
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
        var content = new VerticalStackLayout { Spacing = 12 };

        // Header: title gets the visual priority; status is a compact, consistent chip.
        var title = new Label
        {
            Text = task.Title,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");

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

        // Small, scannable metadata chips instead of one long, crowded sentence.
        var tags = new FlexLayout { Direction = Microsoft.Maui.Layouts.FlexDirection.Row, Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        tags.Children.Add(CreateTaskChip(task.Priority, "BgSurfaceAlt", "TextPrimary"));
        if (!string.IsNullOrWhiteSpace(task.Category))
            tags.Children.Add(CreateTaskChip(task.Category, "BgSurfaceAlt", "TextPrimary"));
        if (task.IsOverdue)
            tags.Children.Add(CreateTaskChip("OVERDUE", "BgGlow", "AccentDanger"));
        if (task.DueDate is null && task.Status is not ("Completed" or "Cancelled" or "Declined" or "Skipped"))
            tags.Children.Add(CreateTaskChip("DUE DATE NEEDED", "BgGlow", "AccentDanger"));
        if (task.IsRecurring)
            tags.Children.Add(CreateTaskChip(
                task.RecurrenceFrequency is { Length: > 0 } frequency ? $"↻ {frequency}" : "↻ RECURRING",
                "BgSurfaceAlt", "TextPrimary"));
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
            description.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            content.Children.Add(description);
        }

        // Two-column information grid keeps assignment and timing easy to scan.
        var info = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            RowSpacing = 10
        };
        var infoIndex = 0;
        if (_showFamilyTasks && !string.IsNullOrWhiteSpace(task.AssignedToName))
            AddTaskInfo(info, infoIndex++, "ASSIGNED TO", task.AssignedToName);
        if (task.DueDate is { } due)
            AddTaskInfo(info, infoIndex++, "DUE", due.ToLocalTime().ToString("ddd, MMM d · h:mm tt", CultureInfo.CurrentCulture));
        else if (task.Status is not ("Completed" or "Cancelled" or "Declined" or "Skipped"))
            AddTaskInfo(info, infoIndex++, "DUE", "Needs a due date");
        if (!string.IsNullOrWhiteSpace(task.PlannedStartTime) || !string.IsNullOrWhiteSpace(task.PlannedEndTime))
            AddTaskInfo(info, infoIndex++, "SCHEDULED", $"{FormatPlannedTime(task.PlannedStartTime)} – {FormatPlannedTime(task.PlannedEndTime)}");
        else if (task.EstimatedDurationMinutes is { } estimate)
            AddTaskInfo(info, infoIndex++, "ESTIMATED", $"{estimate} minutes");
        if (task.ActualDurationMinutes is { } actual)
            AddTaskInfo(info, infoIndex++, "TIME SPENT", $"{actual} minutes");
        if (task.VarianceMinutes is { } variance)
            AddTaskInfo(info, infoIndex++, "SCHEDULE VARIANCE",
                variance == 0 ? "On plan" : variance > 0 ? $"+{variance} min" : $"{variance} min");
        if (infoIndex > 0)
            content.Children.Add(info);

        var actions = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center
        };
        var allActions = ActionsFor(task.Status);
        var visibleActions = allActions
            .Where(x => x.primary || x.action is "complete" or "pause" or "resume")
            .ToArray();
        foreach (var (action, label, primary) in visibleActions)
            actions.Children.Add(MakeActionButton(task, action, label, primary));

        var isTerminal = task.Status is "Completed" or "Cancelled" or "Declined" or "Skipped";
        var canEdit = _showFamilyTasks && !isTerminal;
        var canPlan = task.Status is "Accepted" or "InProgress" or "Paused" or "Deferred";
        var canAssign = _showFamilyTasks &&
            task.Status is ("Created" or "Assigned" or "Reassigned" or "Declined");
        var moreOptions = allActions
            .Where(x => !visibleActions.Any(v => v.action == x.action))
            .Select(x => (x.label, x.action))
            .ToList();
        if (canEdit) moreOptions.Add(("Edit details", "edit"));
        if (canPlan) moreOptions.Add(("Schedule", "schedule"));
        if (canAssign) moreOptions.Add(("Assign", "assign"));
        moreOptions.Add(("History", "history"));
        actions.Children.Add(MakeMoreButton(task, moreOptions));
        var separator = new BoxView { HeightRequest = 1, Opacity = 0.55 };
        separator.SetDynamicResource(VisualElement.BackgroundColorProperty, "BorderSubtle");
        content.Children.Add(separator);
        content.Children.Add(actions);

        var card = new Border
        {
            Padding = new Thickness(16, 15),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = content
        };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _items.Children.Add(card);
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
            CharacterSpacing = 0.25,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        label.SetDynamicResource(Label.TextColorProperty, foregroundResource);
        var chip = new Border
        {
            Padding = new Thickness(9, 5),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Content = label,
            Margin = new Thickness(2),
            VerticalOptions = LayoutOptions.Center
        };
        chip.SetDynamicResource(VisualElement.BackgroundColorProperty, backgroundResource);
        return chip;
    }

    private static void AddTaskInfo(Grid grid, int index, string label, string value)
    {
        var caption = new Label { Text = label, FontSize = 9, CharacterSpacing = 0.7 };
        caption.SetDynamicResource(Label.TextColorProperty, "TextMuted");
        var text = new Label
        {
            Text = value,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        };
        text.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var stack = new VerticalStackLayout { Spacing = 2, Children = { caption, text } };
        grid.Add(stack, index % 2, index / 2);
    }

    private static string NormalizeTaskStatus(string? status) =>
        System.Text.RegularExpressions.Regex.Replace(
            status?.Trim() ?? string.Empty,
            @"[\\s_-]+",
            string.Empty);

    private static (string action, string label, bool primary)[] ActionsFor(string status) =>
        NormalizeTaskStatus(status) switch
        {
            "Assigned" or "Reassigned" => new[]
            {
                ("accept", "Accept Task", true),
                ("decline", "Decline", false),
                ("cancel", "Cancel", false)
            },
            "Accepted" => new[]
            {
                ("start", "Start Task", true),
                ("complete", "Complete Task", false),
                ("decline", "Decline", false),
                ("defer", "Defer", false),
                ("skip", "Skip", false),
                ("cancel", "Cancel", false)
            },
            "InProgress" => new[]
            {
                ("pause", "Pause", false),
                ("complete", "Complete Task", true),
                ("defer", "Defer", false),
                ("cancel", "Cancel", false)
            },
            "Paused" => new[]
            {
                ("resume", "Resume Task", true),
                ("complete", "Complete Task", false),
                ("cancel", "Cancel", false)
            },
            "Deferred" => new[]
            {
                ("start", "Start Task", true),
                ("skip", "Skip", false),
                ("cancel", "Cancel", false)
            },
            _ => Array.Empty<(string, string, bool)>()
        };

    private Button MakeMoreButton(FamilyTaskDto task, List<(string label, string action)> options)
    {
        var button = new Button
        {
            Text = "More",
            FontSize = 12,
            HeightRequest = 36,
            CornerRadius = 18,
            Padding = new Thickness(13, 0),
            Margin = new Thickness(0, 2, 8, 2)
        };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        button.Clicked += async (_, _) =>
        {
            var labels = options.Select(o => o.label).ToArray();
            var selected = await DisplayActionSheetAsync("Task actions", "Close", null, labels);
            var choice = options.FirstOrDefault(o => o.label == selected);
            if (choice.action is null) return;

            switch (choice.action)
            {
                case "edit": await EditTaskAsync(task); break;
                case "schedule": await SetPlannedTimesAsync(task); break;
                case "assign": await ReassignTaskAsync(task); break;
                case "history": await ShowHistoryAsync(task.Id, task.Title); break;
                default: await ExecuteAsync(task, choice.action); break;
            }
        };
        return button;
    }

    private Button MakeActionButton(FamilyTaskDto task, string action, string label, bool primary)
    {
        var button = new Button { Text = label, FontSize = 13, FontAttributes = FontAttributes.Bold, HeightRequest = 44, MinimumWidthRequest = 112, CornerRadius = 22, Padding = new Thickness(16, 0), Margin = new Thickness(0, 3, 8, 3) };
        button.SetDynamicResource(VisualElement.StyleProperty, primary ? "FoPrimaryButtonStyle" : "FoSecondaryButtonStyle");
        button.Clicked += async (_, _) => await ExecuteAsync(task, action);
        return button;
    }

    private Button MakeDetailsButton(FamilyTaskDto task)
    {
        var button = new Button { Text = "Edit", FontSize = 12, HeightRequest = 36, CornerRadius = 18, Padding = new Thickness(13, 0), Margin = new Thickness(0, 2, 8, 2) };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        button.Clicked += async (_, _) => await EditTaskAsync(task);
        return button;
    }

    private Button MakeScheduleButton(FamilyTaskDto task)
    {
        var button = new Button { Text = "Schedule", FontSize = 12, HeightRequest = 36, CornerRadius = 18, Padding = new Thickness(13, 0), Margin = new Thickness(0, 2, 8, 2) };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        button.Clicked += async (_, _) => await SetPlannedTimesAsync(task);
        return button;
    }

    private async Task SetPlannedTimesAsync(FamilyTaskDto task)
    {
        await ShowPlannedScheduleFormAsync(task.Id,
            ParsePlannedDateTime(task.PlannedStartTime),
            ParsePlannedDateTime(task.PlannedEndTime),
            task.EstimatedDurationMinutes);
    }

    private async Task<bool> ShowPlannedScheduleFormAsync(
        Guid taskId, DateTime? startInitial, DateTime? endInitial, int? estimateInitial)
    {
        var startEnabled = new Switch { IsToggled = startInitial.HasValue };
        var endEnabled = new Switch { IsToggled = endInitial.HasValue };
        var startDate = new DatePicker { Date = (startInitial ?? DateTime.Today.AddHours(9)).Date, Format = "ddd, MMM d, yyyy" };
        var startTime = new TimePicker { Time = (startInitial ?? DateTime.Today.AddHours(9)).TimeOfDay, Format = "h:mm tt" };
        var endDate = new DatePicker { Date = (endInitial ?? DateTime.Today.AddHours(10)).Date, Format = "ddd, MMM d, yyyy" };
        var endTime = new TimePicker { Time = (endInitial ?? DateTime.Today.AddHours(10)).TimeOfDay, Format = "h:mm tt" };
        var estimate = new Entry { Placeholder = "Estimated minutes (optional)", Keyboard = Keyboard.Numeric,
            MaxLength = 5, Text = estimateInitial?.ToString(CultureInfo.InvariantCulture) ?? "" };
        var completion = new TaskCompletionSource<bool>();
        var page = new ContentPage { Title = "Plan task time" };

        static View DateTimeField(string label, Switch enabled, DatePicker date, TimePicker time)
        {
            var header = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                Children = { new Label { Text = label, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, enabled }
            };
            var controls = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 10,
                Children = { date, time }
            };
            date.SetBinding(VisualElement.IsEnabledProperty, new Binding(nameof(Switch.IsToggled), source: enabled));
            time.SetBinding(VisualElement.IsEnabledProperty, new Binding(nameof(Switch.IsToggled), source: enabled));
            return new VerticalStackLayout { Spacing = 6, Children = { header, controls } };
        }

        var cancel = new Button { Text = "Not now" };
        cancel.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        cancel.Clicked += async (_, _) => { completion.TrySetResult(false); await Navigation.PopModalAsync(); };
        var save = new Button { Text = "Save schedule" };
        save.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        save.Clicked += async (_, _) =>
        {
            DateTime? start = startEnabled.IsToggled ? startDate.Date + startTime.Time : null;
            DateTime? end = endEnabled.IsToggled ? endDate.Date + endTime.Time : null;
            if (start.HasValue != end.HasValue)
            {
                await DisplayAlertAsync("Incomplete schedule", "Choose both a planned start and end, or turn both off.", "OK");
                return;
            }
            if (start.HasValue && end.HasValue && end.Value <= start.Value)
            {
                await DisplayAlertAsync("Invalid schedule", "The planned end must be after the planned start.", "OK");
                return;
            }
            int? minutes = null;
            if (!string.IsNullOrWhiteSpace(estimate.Text))
            {
                if (!int.TryParse(estimate.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed is < 1 or > 10080)
                {
                    await DisplayAlertAsync("Invalid estimate", "Enter a whole number from 1 to 10080 minutes.", "OK");
                    return;
                }
                minutes = parsed;
            }
            try
            {
                await _tasks.SetPlannedTimesAsync(taskId, new PlannedTimesBody(
                    start?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    end?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    minutes));
                completion.TrySetResult(true);
                await Navigation.PopModalAsync();
            }
            catch (FamilyApiException ex) { await DisplayAlertAsync("Couldn't save schedule", ex.Message, "OK"); }
            catch (Exception) { await DisplayAlertAsync("Couldn't save schedule", "Please try again.", "OK"); }
        };

        page.Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20, Spacing = 16,
                Children =
                {
                    new Label { Text = "Choose when this task should happen", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    DateTimeField("Planned start", startEnabled, startDate, startTime),
                    DateTimeField("Planned end", endEnabled, endDate, endTime),
                    new Label { Text = "Estimated duration (minutes)", FontSize = 12 },
                    estimate,
                    new HorizontalStackLayout { Spacing = 10, Children = { cancel, save } }
                }
            }
        };
        await Navigation.PushModalAsync(new NavigationPage(page));
        var saved = await completion.Task;
        if (saved)
        {
            _status.Text = "Task schedule updated.";
            await LoadAsync();
        }
        return saved;
    }

    private static DateTime? ParsePlannedDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.ToLocalTime().DateTime;
        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var time))
            return DateTime.Today.Add(time.ToTimeSpan());
        return null;
    }

    private static string FormatPlannedTime(string? value)
        => ParsePlannedDateTime(value)?.ToString("ddd, MMM d · h:mm tt") ?? "?";

    private async Task<(bool Confirmed, DateTime? Value)> PickDateTimeAsync(
        string title, DateTime initialValue, bool allowClear)
    {
        var completion = new TaskCompletionSource<(bool Confirmed, DateTime? Value)>();
        var date = new DatePicker { Date = initialValue.Date, Format = "ddd, MMM d, yyyy" };
        var time = new TimePicker { Time = initialValue.TimeOfDay, Format = "h:mm tt" };
        var page = new ContentPage { Title = title, BackgroundColor = Colors.Transparent };
        var modal = new NavigationPage(page);
        var cancel = new Button { Text = "Cancel" };
        cancel.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        cancel.Clicked += async (_, _) =>
        {
            completion.TrySetResult((false, null));
            await modal.Navigation.PopModalAsync();
        };
        var save = new Button { Text = "Use this time" };
        save.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        save.Clicked += async (_, _) =>
        {
            var pickedDate = date.Date is DateTime selectedDate ? selectedDate.Date : initialValue.Date;
            var pickedTime = time.Time is TimeSpan selectedTime ? selectedTime : initialValue.TimeOfDay;
            completion.TrySetResult((true, pickedDate + pickedTime));
            await Navigation.PopModalAsync();
        };
        var buttons = new HorizontalStackLayout { Spacing = 10, Children = { cancel, save } };
        if (allowClear)
        {
            var clear = new Button { Text = "Clear" };
            clear.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
            clear.Clicked += async (_, _) =>
            {
                completion.TrySetResult((true, null));
                await Navigation.PopModalAsync();
            };
            buttons.Children.Insert(1, clear);
        }
        page.Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 18,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, FontSize = 22, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Choose a date" }, date,
                new Label { Text = "Choose a time" }, time,
                buttons
            }
        };
        await Navigation.PushModalAsync(modal);
        return await completion.Task;
    }

    private Button MakeAssignButton(FamilyTaskDto task)
    {
        var button = new Button { Text = "Assign", FontSize = 12, HeightRequest = 36, CornerRadius = 18, Padding = new Thickness(13, 0), Margin = new Thickness(0, 2, 8, 2) };
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
        var priority = await DisplayActionSheetAsync("Priority", "Keep current", null, "Low", "Normal", "High", "Urgent");
        if (priority is null or "Cancel") return;
        if (priority == "Keep current") priority = task.Priority;
        var duePick = await PickDateTimeAsync("Due date and time",
            task.DueDate?.ToLocalTime() ?? DateTime.Today.AddHours(17),
            allowClear: !task.AssignedToMemberId.HasValue);
        if (!duePick.Confirmed) return;
        DateTime? dueDate = duePick.Value?.ToUniversalTime();
        var clearDueDate = !dueDate.HasValue;

        try
        {
            await _tasks.UpdateDetailsAsync(task.Id, new UpdateTaskDetailsBody(
                Title: title.Trim(),
                Description: string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Priority: priority,
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

            var choices = members.Select((m, i) => $"{i + 1}. {m.DisplayName} ({m.Role})").ToArray();
            var selected = await DisplayActionSheetAsync("Assign task to", "Cancel", null, choices);
            var selectedIndex = Array.IndexOf(choices, selected);
            if (selectedIndex < 0) return;
            var member = members[selectedIndex];

            if (task.DueDate is null)
            {
                var duePick = await PickDateTimeAsync("Set due date before assigning",
                    DateTime.Today.AddDays(1).AddHours(17), allowClear: false);
                if (!duePick.Confirmed || !duePick.Value.HasValue) return;
                await _tasks.UpdateDetailsAsync(task.Id, new UpdateTaskDetailsBody(
                    DueDate: duePick.Value.Value.ToUniversalTime()));
            }

            await _tasks.ReassignAsync(task.Id, member.Id);
            _status.Text = $"Task assigned to {member.DisplayName}.";
            await LoadAsync();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not reassign this task."; }
    }

    private Button MakeHistoryButton(Guid taskId, string title)
    {
        var button = new Button { Text = "History", FontSize = 12, HeightRequest = 36, CornerRadius = 18, Padding = new Thickness(13, 0), Margin = new Thickness(0, 2, 8, 2) };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        button.Clicked += async (_, _) => await ShowHistoryAsync(taskId, title);
        return button;
    }

    private async Task ExecuteAsync(FamilyTaskDto task, string action)
    {
        try
        {
            if (action is "defer" or "skip" or "cancel" or "decline")
            {
                var prompt = action switch
                {
                    "defer" => "Optional note for deferring this task:",
                    "skip" => "Optional note for skipping this instance:",
                    "decline" => "Why are you declining this task? (optional)",
                    _ => "Optional reason for cancelling:"
                };
                var note = await DisplayPromptAsync(action[..1].ToUpper() + action[1..], prompt, "Confirm", "Cancel", maxLength: 200);
                // null = user cancelled dialog
                if (note is null)
                    return;

                _ = action switch
                {
                    "defer" => await _tasks.DeferAsync(task.Id, string.IsNullOrWhiteSpace(note) ? null : note),
                    "skip" => await _tasks.SkipAsync(task.Id, string.IsNullOrWhiteSpace(note) ? null : note),
                    "cancel" => await _tasks.CancelAsync(task.Id, string.IsNullOrWhiteSpace(note) ? null : note),
                    "decline" => await _tasks.DeclineAsync(task.Id, string.IsNullOrWhiteSpace(note) ? null : note),
                    _ => throw new InvalidOperationException()
                };
            }
            else
            {
                if (action == "accept" && task.DueDate is null)
                {
                    await DisplayAlertAsync(
                        "Due date needed",
                        "A due date is required before this task can be accepted. Ask a parent or owner to use More → Edit details and set the due date, then tap Accept Task again.",
                        "OK");
                    return;
                }

                // Acceptance is a simple ownership transition. Scheduling stays optional
                // under More → Schedule so it cannot interrupt the Accept → Start → Complete flow.
                await _tasks.RunActionAsync(task.Id, action);
            }

            if (action == "complete")
                _pulseRefresh.RequestRefresh();

            _status.Text = action switch
            {
                "accept" => "Task accepted.",
                "start" => "Task started.",
                "pause" => "Task paused.",
                "resume" => "Task resumed.",
                "complete" => "Task completed.",
                "defer" => "Task deferred.",
                "skip" => "Task instance skipped.",
                "cancel" => "Task cancelled.",
                "decline" => "Task declined.",
                _ => "Task updated."
            };
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

    private async Task ConfigureAcceptedTaskScheduleAsync(Guid taskId)
    {
        await ShowPlannedScheduleFormAsync(taskId,
            DateTime.Today.AddHours(9), DateTime.Today.AddHours(10), null);
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
                .OrderByDescending(h => h.TimestampUtc)
                .Take(20)
                .Select(h =>
                {
                    var who = string.IsNullOrWhiteSpace(h.ActorName) ? "" : $" · {h.ActorName}";
                    var detail = string.IsNullOrWhiteSpace(h.Detail) ? "" : $" — {h.Detail}";
                    return $"{h.TimestampUtc.ToLocalTime():g} · {h.Status}{who}{detail}";
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
        try
        {
            var family = await _family.GetFamilyAsync();
            var members = family.Members.Where(m => m.IsActive).OrderBy(m => m.DisplayName).ToList();

            var title = new Entry { Placeholder = "Task title", MaxLength = 120 };
            var description = new Editor { Placeholder = "Instructions or context (optional)", AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 88, MaxLength = 1000 };
            var category = new Entry { Placeholder = "e.g. Chores, School, Home", MaxLength = 60 };
            var priority = new Picker { Title = "Priority", ItemsSource = new List<string> { "Low", "Normal", "High", "Urgent" }, SelectedIndex = 1 };
            var assignee = new Picker { Title = "Assign to", ItemsSource = new List<string> { "Leave unassigned" }.Concat(members.Select(m => m.DisplayName)).ToList(), SelectedIndex = 0 };
            var estimate = new Entry { Placeholder = "Estimated minutes (optional)", Keyboard = Keyboard.Numeric, MaxLength = 5 };
            var dueEnabled = new Switch { IsToggled = true };
            var dueDate = new DatePicker { Date = DateTime.Today.AddDays(1), Format = "ddd, MMM d, yyyy" };
            var dueTime = new TimePicker { Time = new TimeSpan(17, 0, 0), Format = "h:mm tt" };
            var plannedStartEnabled = new Switch { IsToggled = false };
            var plannedStartDate = new DatePicker { Date = DateTime.Today, Format = "ddd, MMM d, yyyy" };
            var plannedStartTime = new TimePicker { Time = new TimeSpan(9, 0, 0), Format = "h:mm tt" };
            var plannedEndEnabled = new Switch { IsToggled = false };
            var plannedEndDate = new DatePicker { Date = DateTime.Today, Format = "ddd, MMM d, yyyy" };
            var plannedEndTime = new TimePicker { Time = new TimeSpan(10, 0, 0), Format = "h:mm tt" };

            static View Field(string label, View control)
            {
                var stack = new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = label, FontSize = 12 }, control } };
                return stack;
            }
            static View DateTimeField(string label, Switch enabled, DatePicker date, TimePicker time)
            {
                var header = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Children = { new Label { Text = label, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, enabled }
                };
                var controls = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 10,
                    Children = { date, time }
                };
                date.SetBinding(VisualElement.IsEnabledProperty, new Binding(nameof(Switch.IsToggled), source: enabled));
                time.SetBinding(VisualElement.IsEnabledProperty, new Binding(nameof(Switch.IsToggled), source: enabled));
                return new VerticalStackLayout { Spacing = 6, Children = { header, controls } };
            }

            var completion = new TaskCompletionSource<bool>();
            var page = new ContentPage { Title = "New task" };
            var cancel = new Button { Text = "Cancel" };
            cancel.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            cancel.Clicked += async (_, _) => { completion.TrySetResult(false); await Navigation.PopModalAsync(); };
            var save = new Button { Text = "Create task" };
            save.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
            save.Clicked += async (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(title.Text))
                {
                    await DisplayAlertAsync("Task title required", "Enter a title for this task.", "OK");
                    return;
                }
                if (!int.TryParse(estimate.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && !string.IsNullOrWhiteSpace(estimate.Text))
                {
                    await DisplayAlertAsync("Invalid estimate", "Enter a whole number of minutes.", "OK");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(estimate.Text) && minutes is < 1 or > 10080)
                {
                    await DisplayAlertAsync("Invalid estimate", "Estimated minutes must be from 1 to 10080.", "OK");
                    return;
                }
                DateTime? startAt = plannedStartEnabled.IsToggled ? plannedStartDate.Date + plannedStartTime.Time : null;
                DateTime? endAt = plannedEndEnabled.IsToggled ? plannedEndDate.Date + plannedEndTime.Time : null;
                if (startAt.HasValue != endAt.HasValue)
                {
                    await DisplayAlertAsync("Incomplete schedule", "Choose both a planned start and end, or leave both off.", "OK");
                    return;
                }
                if (startAt.HasValue && endAt.HasValue && endAt.Value <= startAt.Value)
                {
                    await DisplayAlertAsync("Invalid schedule", "The planned end must be after the planned start.", "OK");
                    return;
                }

                var selectedMemberIndex = assignee.SelectedIndex - 1;
                Guid? assigneeId = selectedMemberIndex >= 0 && selectedMemberIndex < members.Count
                    ? members[selectedMemberIndex].Id : null;
                if (assigneeId.HasValue && !dueEnabled.IsToggled)
                {
                    await DisplayAlertAsync("Due date required", "Assigned tasks need a due date so they appear in the right order on Family Pulse.", "OK");
                    return;
                }
                try
                {
                    await _tasks.CreateTaskAsync(new CreateTaskBody(
                        title.Text.Trim(),
                        Description: string.IsNullOrWhiteSpace(description.Text) ? null : description.Text.Trim(),
                        Priority: priority.SelectedItem?.ToString() ?? "Normal",
                        Category: string.IsNullOrWhiteSpace(category.Text) ? null : category.Text.Trim(),
                        DueDate: dueEnabled.IsToggled ? ((dueDate.Date ?? DateTime.Today) + (dueTime.Time ?? TimeSpan.Zero)).ToUniversalTime() : null,
                        EstimatedDurationMinutes: string.IsNullOrWhiteSpace(estimate.Text) ? null : minutes,
                        AssignToMemberId: assigneeId,
                        PlannedStartTime: startAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                        PlannedEndTime: endAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
                    completion.TrySetResult(true);
                    await Navigation.PopModalAsync();
                }
                catch (FamilyApiException ex)
                {
                    await DisplayAlertAsync("Couldn't create task", ex.Message, "OK");
                }
                catch (Exception)
                {
                    await DisplayAlertAsync("Couldn't create task", "Please check your connection and try again.", "OK");
                }
            };

            var content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    Field("Title", title),
                    Field("Description", description),
                    new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10,
                        Children = { Field("Priority", priority), Field("Category", category) } },
                    DateTimeField("Due date and time", dueEnabled, dueDate, dueTime),
                    DateTimeField("Planned start", plannedStartEnabled, plannedStartDate, plannedStartTime),
                    DateTimeField("Planned end", plannedEndEnabled, plannedEndDate, plannedEndTime),
                    Field("Estimated duration (minutes)", estimate),
                    Field("Assign to", assignee),
                    new HorizontalStackLayout { Spacing = 10, Children = { cancel, save } }
                }
            };
            page.Content = new ScrollView { Content = content };
            await Navigation.PushModalAsync(new NavigationPage(page));
            var created = await completion.Task;
            if (created)
            {
                _status.Text = "Task created.";
                await LoadAsync();
            }
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not open the new task form."; }
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
