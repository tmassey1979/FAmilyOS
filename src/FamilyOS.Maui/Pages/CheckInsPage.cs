using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Household check-ins. A check-in never requests or enables location sharing.</summary>
public sealed class CheckInsPage : ContentPage
{
    private readonly FamilyApiClient _api;
    private readonly Picker _memberPicker = new() { Title = "Choose a household member" };
    private readonly DatePicker _dueDate = new();
    private readonly TimePicker _dueTime = new();
    private readonly Button _send = new() { Text = "Request check-in" };
    private readonly Button _refresh = new() { Text = "Refresh" };
    private readonly Label _status = new() { FontSize = 13 };
    private readonly VerticalStackLayout _items = new() { Spacing = 10 };
    private List<FamilyMemberDto> _members = [];
    private Guid _currentMemberId;
    private bool _busy;

    public CheckInsPage(FamilyApiClient api)
    {
        _api = api;
        Title = "Check-ins";
        _dueDate.Date = DateTime.Today;
        _dueTime.Time = DateTime.Now.AddHours(2).TimeOfDay;
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _send.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _send.Clicked += async (_, _) => await CreateAsync();
        _refresh.Clicked += async (_, _) => await LoadAsync();

        var heading = new Label { Text = "Household check-ins", FontSize = 25, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var note = new Label
        {
            Text = "Ask someone to confirm they're okay by a certain time. A check-in does not collect or share anyone's location.",
            LineBreakMode = LineBreakMode.WordWrap
        };
        note.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        var form = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Request a check-in", FontAttributes = FontAttributes.Bold },
                    _memberPicker,
                    new Label { Text = "Due date" }, _dueDate,
                    new Label { Text = "Due time" }, _dueTime,
                    _send
                }
            }
        };
        form.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        Content = new RefreshView
        {
            Command = new Command(async () => await LoadAsync()),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(20, 14),
                    Spacing = 14,
                    Children = { heading, note, form, new HorizontalStackLayout { Spacing = 8, Children = { _refresh } }, _status, _items }
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_members.Count == 0 && !_busy)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        _busy = true;
        _send.IsEnabled = _refresh.IsEnabled = false;
        _status.Text = "Loading check-ins…";
        try
        {
            var family = await _api.GetFamilyAsync();
            _currentMemberId = family.CurrentMemberId;
            _members = family.Members.Where(x => x.IsActive && x.Id != _currentMemberId).ToList();
            _memberPicker.ItemsSource = _members.Select(x => x.DisplayName).ToList();
            if (_members.Count > 0 && _memberPicker.SelectedIndex < 0)
                _memberPicker.SelectedIndex = 0;
            var rows = await _api.GetCheckInsAsync();
            _items.Children.Clear();
            if (rows.Count == 0)
                AddInfo("No check-ins yet", "Requests you send or receive will appear here.");
            else
                foreach (var item in rows.OrderByDescending(x => x.CreatedAtUtc))
                    AddCheckIn(item);
            _status.Text = $"{rows.Count(x => x.Status is "Pending" or "Overdue")} open · {rows.Count} total";
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not load check-ins. Check your connection and try again."; }
        finally { _busy = false; _send.IsEnabled = _members.Count > 0; _refresh.IsEnabled = true; }
    }

    private async Task CreateAsync()
    {
        if (_busy || _memberPicker.SelectedIndex < 0 || _memberPicker.SelectedIndex >= _members.Count)
            return;

        // MAUI 11: DatePicker.Date is DateTime?, TimePicker.Time is TimeSpan?.
        DateTime dueDate = DateTime.Today;
        if (_dueDate.Date is DateTime pickedDate)
            dueDate = pickedDate.Date;

        TimeSpan dueTimeOfDay = DateTime.Now.AddHours(2).TimeOfDay;
        if (_dueTime.Time is TimeSpan pickedTime)
            dueTimeOfDay = pickedTime;

        DateTime due = dueDate + dueTimeOfDay;
        var dueLocal = new DateTimeOffset(DateTime.SpecifyKind(due, DateTimeKind.Local));
        if (dueLocal <= DateTimeOffset.Now || dueLocal > DateTimeOffset.Now.AddDays(7))
        {
            _status.Text = "Choose a future due time within the next seven days.";
            return;
        }
        _busy = true;
        _send.IsEnabled = false;
        try
        {
            await _api.CreateCheckInAsync(_members[_memberPicker.SelectedIndex].Id, dueLocal.ToUniversalTime());
            _status.Text = "Check-in request sent.";
            await LoadAfterActionAsync();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not send the check-in request."; }
        finally { _busy = false; _send.IsEnabled = _members.Count > 0; }
    }

    private async Task LoadAfterActionAsync()
    {
        try
        {
            var rows = await _api.GetCheckInsAsync();
            _items.Children.Clear();
            foreach (var item in rows.OrderByDescending(x => x.CreatedAtUtc))
                AddCheckIn(item);
            _status.Text = $"{rows.Count(x => x.Status is "Pending" or "Overdue")} open · {rows.Count} total";
        }
        catch { _status.Text = "Action completed, but the list could not be refreshed."; }
    }

    private void AddCheckIn(CheckInDto item)
    {
        var title = item.CanRespond ? $"From {item.RequesterName}" : $"To {item.TargetName}";
        var stack = new VerticalStackLayout { Spacing = 6 };
        var heading = new Label { Text = title, FontAttributes = FontAttributes.Bold, FontSize = 16 };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(heading);
        var detail = new Label
        {
            Text = $"{item.Status} · due {item.DueAtUtc.ToLocalTime():g}" +
                (string.IsNullOrWhiteSpace(item.ResponseMessage) ? "" : $"\n{item.ResponseMessage}"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        detail.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        stack.Children.Add(detail);

        if (item.CanRespond && item.Status is "Pending" or "Overdue")
        {
            var safe = new Button { Text = "I'm safe" };
            safe.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
            safe.Clicked += async (_, _) => await RespondAsync(item.Id);
            stack.Children.Add(safe);
        }
        if (item.RequesterMemberId == _currentMemberId && item.Status is "Pending" or "Overdue")
        {
            var cancel = new Button { Text = "Cancel request" };
            cancel.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            cancel.Clicked += async (_, _) => await CancelAsync(item.Id);
            stack.Children.Add(cancel);
        }
        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _items.Children.Add(card);
    }

    private async Task RespondAsync(Guid id)
    {
        try
        {
            await _api.RespondToCheckInAsync(id);
            _status.Text = "You marked yourself safe. The requester has been notified.";
            await LoadAfterActionAsync();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch { _status.Text = "Could not submit your response."; }
    }

    private async Task CancelAsync(Guid id)
    {
        try
        {
            await _api.CancelCheckInAsync(id);
            _status.Text = "Check-in cancelled.";
            await LoadAfterActionAsync();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch { _status.Text = "Could not cancel that check-in."; }
    }

    private void AddInfo(string title, string body)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Children.Add(new Label { Text = title, FontAttributes = FontAttributes.Bold });
        stack.Children.Add(new Label { Text = body, LineBreakMode = LineBreakMode.WordWrap });
        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _items.Children.Add(card);
    }
}
