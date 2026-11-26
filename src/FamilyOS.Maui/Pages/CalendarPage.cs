using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>API-backed family calendar with date navigation and event management.</summary>
public sealed class CalendarPage : ContentPage
{
    private readonly CalendarApiClient _calendar;
    private readonly TasksApiClient _tasks;
    private readonly Dictionary<Guid, string> _taskTitles = new();
    private readonly VerticalStackLayout _events;
    private readonly Label _rangeLabel;
    private readonly Label _status;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly Button _today;
    private readonly Button _add;
    private DateTime _rangeStart = DateTime.Today;
    private bool _loading;

    public CalendarPage(CalendarApiClient calendar, TasksApiClient tasks)
    {
        _calendar = calendar;
        _tasks = tasks;
        Title = "Calendar";

        _status = new Label { FontSize = 13 };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _rangeLabel = new Label { FontSize = 16, HorizontalTextAlignment = TextAlignment.Center };
        _rangeLabel.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");

        _previous = new Button { Text = "‹ 14 days", FontSize = 12 };
        _previous.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _previous.Clicked += async (_, _) => { _rangeStart = _rangeStart.AddDays(-14); await LoadAsync(); };
        _next = new Button { Text = "14 days ›", FontSize = 12 };
        _next.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _next.Clicked += async (_, _) => { _rangeStart = _rangeStart.AddDays(14); await LoadAsync(); };
        _today = new Button { Text = "Today", FontSize = 12 };
        _today.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _today.Clicked += async (_, _) => { _rangeStart = DateTime.Today; await LoadAsync(); };
        _add = new Button { Text = "New event" };
        _add.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _add.Clicked += async (_, _) => await CreateEventAsync();

        var toolbar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 6,
            Children = { _previous, _rangeLabel, _next }
        };
        Grid.SetColumn(_rangeLabel, 1);
        Grid.SetColumn(_next, 2);

        _events = new VerticalStackLayout { Spacing = 12 };
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(16, 12),
            Spacing = 14,
            Children = { _add, toolbar, _today, _status, _events }
        };
        Content = new RefreshView
        {
            Content = new ScrollView { Content = content },
            Command = new Command(async () => await LoadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_events.Children.Count == 0 && !_loading)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        SetBusy(true);
        var end = _rangeStart.AddDays(14);
        _rangeLabel.Text = $"{_rangeStart:MMM d} – {end.AddDays(-1):MMM d, yyyy}";
        _status.Text = "Loading family events…";
        try
        {
            var events = await _calendar.GetRangeAsync(_rangeStart, end);
            _taskTitles.Clear();
            try
            {
                var tasks = await _tasks.GetFamilyTasksAsync();
                foreach (var task in tasks)
                    _taskTitles[task.Id] = task.Title;
            }
            catch
            {
                // Calendar events remain useful if task details are temporarily unavailable.
            }
            _events.Children.Clear();
            if (events.Count == 0)
            {
                AddInfoCard("No events in this period", "Move to another date range or add a family event.");
                _status.Text = "Calendar is up to date.";
            }
            else
            {
                _status.Text = $"{events.Count} event{(events.Count == 1 ? "" : "s")}";
                foreach (var group in events.OrderBy(e => e.StartUtc).GroupBy(e => e.StartUtc.ToLocalTime().Date))
                {
                    var heading = new Label { Text = group.Key.ToString("dddd, MMMM d"), FontSize = 16, FontAttributes = FontAttributes.Bold };
                    heading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
                    _events.Children.Add(heading);
                    foreach (var item in group) AddEvent(item);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _status.Text = "The calendar request timed out. Pull to refresh and try again.";
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
            _status.Text = "Something went wrong while loading the family calendar.";
        }
        finally
        {
            _loading = false;
            SetBusy(false);
        }
    }

    private void AddEvent(CalendarEventDto item)
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        var title = new Label { Text = item.Title, FontSize = 17, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(title);

        var start = item.StartUtc.ToLocalTime();
        var when = item.AllDay ? "All day" : start.ToString("h:mm tt");
        if (!item.AllDay && item.EndUtc is not null)
            when += $" – {item.EndUtc.Value.ToLocalTime():h:mm tt}";
        var details = new List<string> { when };
        if (!string.IsNullOrWhiteSpace(item.Location)) details.Add(item.Location);
        var meta = new Label { Text = string.Join(" · ", details), FontSize = 13 };
        meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        stack.Children.Add(meta);

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            var description = new Label { Text = item.Description, FontSize = 14 };
            description.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            stack.Children.Add(description);
        }

        if (item.LinkedTaskId is not null || item.LinkedRequestId is not null)
        {
            var linked = item.LinkedTaskId is Guid taskId
                ? $"Task: {_taskTitles.GetValueOrDefault(taskId, "Linked family task")}"
                : "Linked to a family request";
            var linkLabel = new Label { Text = linked, FontSize = 12 };
            linkLabel.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            stack.Children.Add(linkLabel);
        }

        var actions = new HorizontalStackLayout { Spacing = 8 };
        var edit = new Button { Text = "Edit", FontSize = 12 };
        edit.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        edit.Clicked += async (_, _) => await EditEventAsync(item);
        var cancel = new Button { Text = "Cancel event", FontSize = 12 };
        cancel.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        cancel.Clicked += async (_, _) => await CancelEventAsync(item);
        actions.Children.Add(edit);
        actions.Children.Add(cancel);
        stack.Children.Add(actions);

        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _events.Children.Add(card);
    }

    private async Task CreateEventAsync()
    {
        var title = await DisplayPromptAsync("New event", "Event title");
        if (string.IsNullOrWhiteSpace(title)) return;
        var pickedStart = await PickEventDateTimeAsync("Event start", _rangeStart.AddHours(18));
        if (!pickedStart.HasValue) return;
        var location = await DisplayPromptAsync("Location", "Optional location", "Save", "Skip", maxLength: 160);
        Guid? linkedTaskId = null;
        try
        {
            var tasks = await _tasks.GetFamilyTasksAsync();
            var linkable = tasks.Where(t => t.Status is not ("Completed" or "Cancelled" or "Skipped"))
                .OrderBy(t => t.DueDate ?? DateTime.MaxValue).ThenBy(t => t.Title).ToList();
            if (linkable.Count > 0)
            {
                var choices = linkable.Select(t => $"{t.Title} · {t.Status}").ToArray();
                var selected = await DisplayActionSheetAsync("Link to a task? (Optional)", "No task", "Cancel", choices);
                if (selected is not null && selected != "No task" && selected != "Cancel")
                {
                    var index = Array.IndexOf(choices, selected);
                    if (index >= 0) linkedTaskId = linkable[index].Id;
                }
                if (selected == "Cancel") return;
            }
        }
        catch (FamilyApiException ex)
        {
            await DisplayAlertAsync("Could not load tasks", ex.Message, "Continue without link");
        }

        var start = pickedStart.Value.ToUniversalTime();
        var body = new CalendarEventWriteDto(title.Trim(), start, start.AddHours(1), false,
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(), LinkedTaskId: linkedTaskId);
        await SaveAsync(() => _calendar.CreateAsync(body), "Event created.");
    }

    private async Task EditEventAsync(CalendarEventDto item)
    {
        var title = await DisplayPromptAsync("Edit event", "Event title", initialValue: item.Title, maxLength: 160);
        if (string.IsNullOrWhiteSpace(title)) return;
        var pickedStart = await PickEventDateTimeAsync("Event start", item.StartUtc.ToLocalTime());
        if (!pickedStart.HasValue) return;
        var location = await DisplayPromptAsync("Location", "Optional location", initialValue: item.Location ?? "", maxLength: 160);
        var start = pickedStart.Value.ToUniversalTime();
        var duration = item.EndUtc is null ? TimeSpan.FromHours(1) : item.EndUtc.Value - item.StartUtc;
        if (duration <= TimeSpan.Zero) duration = TimeSpan.FromHours(1);
        var body = new CalendarEventWriteDto(title.Trim(), start, start.Add(duration), item.AllDay,
            string.IsNullOrWhiteSpace(location) ? null : location.Trim());
        await SaveAsync(() => _calendar.UpdateAsync(item.Id, body), "Event updated.");
    }

    private async Task<DateTime?> PickEventDateTimeAsync(string title, DateTime initialValue)
    {
        var completion = new TaskCompletionSource<DateTime?>();
        var date = new DatePicker { Date = initialValue.Date, Format = "ddd, MMM d, yyyy" };
        var time = new TimePicker { Time = initialValue.TimeOfDay, Format = "h:mm tt" };
        var page = new ContentPage { Title = title };
        var cancel = new Button { Text = "Cancel" };
        cancel.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        cancel.Clicked += async (_, _) => { completion.TrySetResult(null); await Navigation.PopModalAsync(); };
        var save = new Button { Text = "Use this time" };
        save.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        save.Clicked += async (_, _) =>
        {
            completion.TrySetResult((date.Date ?? initialValue.Date).Date + (time.Time ?? initialValue.TimeOfDay));
            await Navigation.PopModalAsync();
        };
        page.Content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 16, VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Date" }, date,
                new Label { Text = "Time" }, time,
                new HorizontalStackLayout { Spacing = 10, Children = { cancel, save } }
            }
        };
        await Navigation.PushModalAsync(new NavigationPage(page));
        return await completion.Task;
    }

    private async Task CancelEventAsync(CalendarEventDto item)
    {
        var confirm = await DisplayAlertAsync("Cancel event", $"Cancel “{item.Title}”?", "Cancel event", "Keep event");
        if (!confirm) return;
        try
        {
            await _calendar.CancelAsync(item.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Calendar update failed", ex is FamilyApiException ? ex.Message : "The event could not be cancelled.", "OK");
        }
    }

    private async Task SaveAsync(Func<Task<CalendarEventDto>> save, string successMessage)
    {
        try
        {
            await save();
            await DisplayAlertAsync("Calendar", successMessage, "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Calendar update failed", ex is FamilyApiException ? ex.Message : "The event could not be saved.", "OK");
        }
    }

    private void AddInfoCard(string title, string detail)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        var heading = new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var body = new Label { Text = detail, FontSize = 14 };
        body.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        stack.Children.Add(heading);
        stack.Children.Add(body);
        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _events.Children.Add(card);
    }

    private void SetBusy(bool busy)
    {
        _previous.IsEnabled = !busy;
        _next.IsEnabled = !busy;
        _today.IsEnabled = !busy;
        _add.IsEnabled = !busy;
    }
}
