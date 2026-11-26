using System.Text.Json;
using FamilyOS.Maui.Services;
using Microsoft.Maui.Layouts;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Read and act on the signed-in member's requests and the server-authorized
/// approval queue. All permissions and state transitions remain API-owned.
/// </summary>
public sealed class RequestsPage : ContentPage
{
    private readonly RequestsApiClient _api;
    private readonly VerticalStackLayout _items = new() { Spacing = 12 };
    private readonly Label _status = new() { FontSize = 13 };
    private readonly Button _mineButton = new() { Text = "My requests" };
    private readonly Button _queueButton = new() { Text = "Family queue" };
    private readonly Button _refreshButton = new() { Text = "Refresh" };
    private readonly Button _createButton = new() { Text = "New request" };
    private bool _showQueue;
    private bool _loading;

    public RequestsPage(RequestsApiClient api)
    {
        _api = api;
        Title = "Requests";
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _mineButton.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _queueButton.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _refreshButton.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _createButton.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _mineButton.Clicked += async (_, _) => { _showQueue = false; await LoadAsync(); };
        _queueButton.Clicked += async (_, _) => { _showQueue = true; await LoadAsync(); };
        _refreshButton.Clicked += async (_, _) => await LoadAsync();
        _createButton.Clicked += async (_, _) => await CreateRequestAsync();

        var heading = new Label { Text = "Requests", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var tabs = new HorizontalStackLayout { Spacing = 8, Children = { _mineButton, _queueButton } };
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Children = { heading, _refreshButton }
        };
        Grid.SetColumn(_refreshButton, 1);
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12),
            Spacing = 16,
            Children = { header, tabs, _createButton, _status, _items }
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
        if (_items.Children.Count == 0 && !_loading)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _refreshButton.IsEnabled = false;
        _createButton.IsEnabled = false;
        _mineButton.IsEnabled = false;
        _queueButton.IsEnabled = false;
        _status.Text = "Loading requests…";
        try
        {
            var rows = _showQueue
                ? await _api.GetApprovalQueueAsync()
                : await _api.GetMineAsync();
            _items.Children.Clear();
            if (rows.Count == 0)
            {
                AddCard(_showQueue ? "No family requests to review" : "No requests yet",
                    _showQueue ? "New submissions and approved requests ready for follow-up will appear here." : "Requests you create will appear here.");
                _status.Text = "You're all caught up.";
            }
            else
            {
                _status.Text = $"{rows.Count} request{(rows.Count == 1 ? "" : "s")}";
                foreach (var request in rows.OrderByDescending(x => x.CreatedAtUtc))
                    await AddRequestAsync(request);
            }
            UpdateTabs();
        }
        catch (OperationCanceledException)
        {
            _status.Text = "The request timed out. Pull to refresh and try again.";
        }
        catch (FamilyApiException ex)
        {
            _items.Children.Clear();
            _status.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            _status.Text = "FamilyOS could not be reached. Check your connection and try again.";
        }
        catch (Exception)
        {
            _status.Text = "Something went wrong while loading requests.";
        }
        finally
        {
            _loading = false;
            _refreshButton.IsEnabled = true;
            _createButton.IsEnabled = true;
            _mineButton.IsEnabled = true;
            _queueButton.IsEnabled = true;
        }
    }

    private async Task CreateRequestAsync()
    {
        if (_loading) return;
        _createButton.IsEnabled = false;
        try
        {
            var types = await _api.GetTypesAsync();
            if (types.Count == 0)
            {
                _status.Text = "No request types are currently available.";
                return;
            }

            var selected = await DisplayActionSheetAsync("Choose a request type", "Cancel", null,
                types.Select(x => x.Name).ToArray());
            var type = types.FirstOrDefault(x => string.Equals(x.Name, selected, StringComparison.Ordinal));
            if (type is null) return;

            await ShowCreateRequestFormAsync(type);
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Loading request types timed out. Try again.";
        }
        catch (Exception)
        {
            _status.Text = "The request form could not be opened. Try again.";
        }
        finally
        {
            _createButton.IsEnabled = true;
        }
    }

    private async Task ShowCreateRequestFormAsync(RequestTypeOptionDto type)
    {
        var title = new Entry { Placeholder = "Give your request a short title", MaxLength = 120 };
        var fields = new List<(string Key, string Label, bool Required, int Kind, View Control, TimePicker? Time)>();
        var form = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        form.Children.Add(new Label
        {
            Text = type.Name,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        });
        if (!string.IsNullOrWhiteSpace(type.Description))
            form.Children.Add(new Label { Text = type.Description, FontSize = 13 });
        form.Children.Add(new Label { Text = "Request title", FontAttributes = FontAttributes.Bold });
        form.Children.Add(title);

        foreach (var question in type.Questions.OrderBy(q => ReadInt(q, "sortOrder")))
        {
            var key = ReadString(question, "key");
            var label = ReadString(question, "label");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(label)) continue;

            var required = ReadBool(question, "required");
            var kind = ReadInt(question, "type");
            var placeholder = ReadString(question, "placeholder");
            View control;
            TimePicker? time = null;

            switch (kind)
            {
                case 2:
                case 8:
                    control = new Switch { IsToggled = false, HorizontalOptions = LayoutOptions.Start };
                    break;
                case 3:
                    var options = ReadStringArray(question, "options");
                    var picker = new Picker { Title = placeholder.Length > 0 ? placeholder : "Choose an option" };
                    foreach (var option in options) picker.Items.Add(option);
                    control = picker;
                    break;
                case 4:
                    var dateTimeGrid = new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                        ColumnSpacing = 10
                    };
                    var dateTimeDate = new DatePicker { Date = DateTime.Today, Format = "ddd, MMM d, yyyy" };
                    time = new TimePicker { Time = DateTime.Now.TimeOfDay, Format = "h:mm tt" };
                    dateTimeGrid.Children.Add(dateTimeDate);
                    dateTimeGrid.Children.Add(time);
                    Grid.SetColumn(time, 1);
                    control = dateTimeGrid;
                    break;
                case 10:
                    control = new DatePicker { Date = DateTime.Today, Format = "ddd, MMM d, yyyy" };
                    break;
                case 5:
                    control = new Editor
                    {
                        Placeholder = placeholder.Length > 0 ? placeholder : label,
                        AutoSize = EditorAutoSizeOption.TextChanges,
                        MinimumHeightRequest = 88,
                        MaxLength = 2000
                    };
                    break;
                default:
                    control = new Entry
                    {
                        Placeholder = placeholder.Length > 0 ? placeholder : label,
                        Keyboard = kind is 1 or 9 ? Keyboard.Numeric : Keyboard.Default,
                        MaxLength = 500
                    };
                    break;
            }

            var caption = new Label
            {
                Text = required ? $"{label} *" : label,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14
            };
            form.Children.Add(caption);
            form.Children.Add(control);
            fields.Add((key, label, required, kind, control, time));
        }

        var status = new Label { FontSize = 12 };
        status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        form.Children.Add(status);

        var completion = new TaskCompletionSource<bool>();
        var page = new ContentPage { Title = "New request" };
        var cancel = new Button { Text = "Cancel" };
        cancel.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        cancel.Clicked += async (_, _) =>
        {
            completion.TrySetResult(false);
            await Navigation.PopModalAsync();
        };

        var submit = new Button { Text = "Create request" };
        submit.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        submit.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text))
            {
                status.Text = "A short request title is required.";
                return;
            }

            var answers = new Dictionary<string, object?>();
            decimal? amount = null;
            DateTime? neededBy = null;

            foreach (var field in fields)
            {
                object? value = null;
                switch (field.Kind)
                {
                    case 2:
                    case 8:
                        value = ((Switch)field.Control).IsToggled;
                        break;
                    case 3:
                        value = ((Picker)field.Control).SelectedItem?.ToString();
                        break;
                    case 4:
                        var dateTimeGrid = (Grid)field.Control;
                        var pickedDate = (DatePicker)dateTimeGrid.Children[0];
                        var pickedTime = field.Time!;
                        var dateTime = pickedDate.Date.GetValueOrDefault(DateTime.Today) + pickedTime.Time.GetValueOrDefault(TimeSpan.Zero);
                        value = dateTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                        if (field.Key.Equals("when", StringComparison.OrdinalIgnoreCase)
                            || field.Key.Equals("leave", StringComparison.OrdinalIgnoreCase))
                            neededBy = dateTime.ToUniversalTime();
                        break;
                    case 10:
                        var date = ((DatePicker)field.Control).Date.GetValueOrDefault(DateTime.Today);
                        value = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                        if (field.Key.Equals("due", StringComparison.OrdinalIgnoreCase))
                            neededBy = date.ToUniversalTime();
                        break;
                    case 5:
                        value = ((Editor)field.Control).Text?.Trim();
                        break;
                    case 1:
                    case 9:
                        var numericText = ((Entry)field.Control).Text?.Trim();
                        if (!string.IsNullOrWhiteSpace(numericText))
                        {
                            if (!decimal.TryParse(numericText, System.Globalization.NumberStyles.Number,
                                System.Globalization.CultureInfo.CurrentCulture, out var number))
                            {
                                status.Text = $"{field.Label} must be a valid number.";
                                return;
                            }
                            value = number;
                            if (field.Kind == 9 || field.Key.Equals("amount", StringComparison.OrdinalIgnoreCase))
                                amount = number;
                        }
                        break;
                    default:
                        value = ((Entry)field.Control).Text?.Trim();
                        break;
                }

                var isEmpty = value is null || value is string textValue && string.IsNullOrWhiteSpace(textValue);
                if (isEmpty && field.Required)
                {
                    status.Text = $"{field.Label} is required.";
                    return;
                }
                if (!isEmpty) answers[field.Key] = value;
            }

            if (answers.TryGetValue("where", out var where) && !answers.ContainsKey("location"))
                answers["location"] = where;
            if (answers.TryGetValue("leave", out var leave) && !answers.ContainsKey("when"))
                answers["when"] = leave;
            if (answers.TryGetValue("item", out var item) && !answers.ContainsKey("items"))
                answers["items"] = item;

            submit.IsEnabled = false;
            status.Text = "Creating request…";
            try
            {
                await _api.CreateAsync(type.Code, title.Text.Trim(), answers, amount, neededBy);
                completion.TrySetResult(true);
                await Navigation.PopModalAsync();
            }
            catch (FamilyApiException ex)
            {
                status.Text = ex.Message;
                submit.IsEnabled = true;
            }
            catch (Exception)
            {
                status.Text = "The request could not be created. Check your connection and try again.";
                submit.IsEnabled = true;
            }
        };

        form.Children.Add(new HorizontalStackLayout { Spacing = 10, Children = { cancel, submit } });
        page.Content = new ScrollView { Content = form };
        await Navigation.PushModalAsync(new NavigationPage(page));
        var created = await completion.Task;
        if (created)
        {
            _showQueue = false;
            await LoadAsync();
        }
    }

    private static string ReadString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int ReadInt(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            && value.TryGetInt32(out var number) ? number : 0;

    private static bool ReadBool(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.True;

    private static string[] ReadStringArray(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToArray()
            : Array.Empty<string>();

    private async Task AskQuestionAsync(Guid requestId)
    {
        var question = await DisplayPromptAsync("Ask for more information", "What would you like the requester to clarify?",
            "Send question", "Cancel", maxLength: 1000);
        if (string.IsNullOrWhiteSpace(question)) return;
        try
        {
            await _api.AskQuestionAsync(requestId, question.Trim());
            await LoadAsync();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not send that question. Try again."; }
    }

    private async Task ShowHistoryAsync(Guid requestId, string requestTitle)
    {
        try
        {
            var history = await _api.GetHistoryAsync(requestId);
            var content = new VerticalStackLayout { Padding = 20, Spacing = 12 };
            var heading = new Label { Text = "Request history", FontSize = 23, FontAttributes = FontAttributes.Bold };
            heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
            content.Children.Add(heading);
            content.Children.Add(new Label { Text = requestTitle, FontSize = 15, FontAttributes = FontAttributes.Bold });
            if (history.Count == 0)
                content.Children.Add(new Label { Text = "No history has been recorded yet." });
            foreach (var entry in history.OrderByDescending(x => x.TimestampUtc))
            {
                var detail = new Label { Text = entry.Detail, FontSize = 14 };
                detail.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
                var meta = new Label { Text = $"{entry.Status}  ·  {entry.TimestampUtc.ToLocalTime():MMM d, yyyy h:mm tt}", FontSize = 12 };
                meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
                var row = new VerticalStackLayout { Spacing = 4, Children = { detail, meta } };
                var card = new Border { Padding = 12, Content = row };
                card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
                content.Children.Add(card);
            }
            var close = new Button { Text = "Close" };
            close.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
            close.Clicked += async (_, _) => await Navigation.PopModalAsync();
            content.Children.Add(close);
            await Navigation.PushModalAsync(new NavigationPage(new ContentPage
            {
                Title = "History", Content = new ScrollView { Content = content }
            }));
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not load request history. Try again."; }
    }

    private async Task AnswerQuestionAsync(Guid requestId, Guid questionId, string prompt)
    {
        var answer = await DisplayPromptAsync("Request information", prompt, "Send answer", "Cancel",
            maxLength: 1000);
        if (string.IsNullOrWhiteSpace(answer)) return;
        try
        {
            await _api.AnswerQuestionAsync(requestId, questionId, answer.Trim());
            await LoadAsync();
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not send that answer. Try again."; }
    }

    private void UpdateTabs()
    {
        _mineButton.Opacity = _showQueue ? 0.65 : 1;
        _queueButton.Opacity = _showQueue ? 1 : 0.65;
    }

    private async Task AddRequestAsync(FamilyRequestDto request)
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        var title = new Label { Text = request.Title, FontSize = 17, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(title);

        var metaParts = new List<string> { request.Type, request.Status, $"By {request.RequesterName}" };
        if (request.Amount is not null) metaParts.Add(request.Amount.Value.ToString("C"));
        var meta = new Label { Text = string.Join(" · ", metaParts), FontSize = 13 };
        meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        stack.Children.Add(meta);

        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            var summary = new Label { Text = request.Summary, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
            summary.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            stack.Children.Add(summary);
        }
        if (!string.IsNullOrWhiteSpace(request.DenialReason))
        {
            var denial = new Label { Text = $"Reason: {request.DenialReason}", FontSize = 13 };
            denial.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            stack.Children.Add(denial);
        }

        foreach (var question in request.PendingQuestions?.Where(q => !q.IsAnswered) ?? Enumerable.Empty<PendingRequestQuestionDto>())
        {
            var prompt = new Label { Text = $"Question: {question.QuestionText}", FontSize = 14 };
            prompt.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            stack.Children.Add(prompt);
            if (!_showQueue)
            {
                var answerButton = new Button { Text = "Answer question", FontSize = 13, HorizontalOptions = LayoutOptions.Start };
                answerButton.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
                answerButton.Clicked += async (_, _) => await AnswerQuestionAsync(request.Id, question.Id, question.QuestionText);
                stack.Children.Add(answerButton);
            }
        }

        if (_showQueue && request.Status is "Submitted" or "PendingApproval" or "UnderReview" or "WaitingForInformation")
        {
            var askButton = new Button { Text = "Ask a question", FontSize = 13, HorizontalOptions = LayoutOptions.Start };
            askButton.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
            askButton.Clicked += async (_, _) => await AskQuestionAsync(request.Id);
            stack.Children.Add(askButton);
        }

        if (request.Status == "ConditionallyApproved")
        {
            try
            {
                var conditions = await _api.GetConditionsAsync(request.Id);
                foreach (var condition in conditions)
                {
                    var conditionLabel = new Label
                    {
                        Text = $"{condition.Name} · {condition.Status}" + (string.IsNullOrWhiteSpace(condition.Description) ? "" : $"\n{condition.Description}"),
                        FontSize = 13
                    };
                    conditionLabel.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
                    stack.Children.Add(conditionLabel);
                    if (condition.Status == "Pending" && !_showQueue)
                    {
                        var satisfy = new Button { Text = "Mark condition complete", FontSize = 13, HorizontalOptions = LayoutOptions.Start };
                        satisfy.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
                        satisfy.Clicked += async (_, _) =>
                        {
                            try { await _api.SatisfyConditionAsync(condition.Id); await LoadAsync(); }
                            catch (FamilyApiException ex) { _status.Text = ex.Message; }
                            catch (Exception) { _status.Text = "Could not update that condition. Try again."; }
                        };
                        stack.Children.Add(satisfy);
                    }
                }
            }
            catch (Exception) { /* Keep the request visible if condition details are temporarily unavailable. */ }
        }

        var actions = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row };
        foreach (var action in ActionsFor(request.Status, _showQueue))
        {
            var button = new Button { Text = action.Label, FontSize = 13, Margin = new Thickness(0, 0, 8, 0) };
            button.SetDynamicResource(VisualElement.StyleProperty,
                action.Primary ? "FoPrimaryButtonStyle" : "FoSecondaryButtonStyle");
            button.Clicked += async (_, _) => await ExecuteAsync(request.Id, action.Name);
            actions.Children.Add(button);
        }
        if (actions.Children.Count > 0) stack.Children.Add(actions);

        var historyButton = new Button { Text = "View history", FontSize = 13, HorizontalOptions = LayoutOptions.Start };
        historyButton.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        historyButton.Clicked += async (_, _) => await ShowHistoryAsync(request.Id, request.Title);
        stack.Children.Add(historyButton);

        if (request.ExecutionPlanId.HasValue)
        {
            var reviewPlan = new Button { Text = "Review execution plan", FontSize = 13 };
            reviewPlan.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
            reviewPlan.Clicked += async (_, _) => await ShowExecutionPlanAsync(request, false);
            stack.Children.Add(reviewPlan);
        }
        else if (request.Status is "Approved" or "ConditionallyApproved" or "Executable")
        {
            var generatePlan = new Button { Text = "Prepare execution plan", FontSize = 13 };
            generatePlan.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
            generatePlan.Clicked += async (_, _) => await ShowExecutionPlanAsync(request, true);
            stack.Children.Add(generatePlan);
        }

        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _items.Children.Add(border);
    }

    private static IReadOnlyList<(string Name, string Label, bool Primary)> ActionsFor(string status, bool queue)
    {
        if (queue && status is "Submitted" or "PendingApproval" or "UnderReview" or "WaitingForInformation")
            return new[] { ("approve", "Approve", true), ("conditional-approve", "Approve with condition", false), ("deny", "Deny", false) };
        if (queue && status == "ConditionallyApproved")
            return new[] { ("add-condition", "Add condition", false) };
        if (!queue && status == "Draft")
            return new[] { ("submit", "Submit", true), ("cancel", "Withdraw", false) };
        if (!queue && status is "Submitted" or "PendingApproval" or "UnderReview" or "WaitingForInformation")
            return new[] { ("cancel", "Withdraw", false) };
        return Array.Empty<(string, string, bool)>();
    }

    private async Task ExecuteAsync(Guid id, string action)
    {
        _status.Text = "Saving request update…";
        try
        {
            if (action is "conditional-approve" or "add-condition")
            {
                if (action == "conditional-approve")
                    await _api.RunActionAsync(id, action);
                var name = await DisplayPromptAsync("Add an approval condition", "What must happen before this request is fully approved?",
                    "Continue", "Cancel", maxLength: 120);
                if (string.IsNullOrWhiteSpace(name)) return;
                var description = await DisplayPromptAsync("Condition details", "Add details (optional)",
                    "Save condition", "Cancel", maxLength: 500);
                if (description is null) return;
                await _api.CreateConditionAsync(id, name.Trim(), description.Trim());
            }
            else
                await _api.RunActionAsync(id, action);
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "The request could not be updated. Refresh and try again.";
        }
    }

    private async Task ShowExecutionPlanAsync(FamilyRequestDto request, bool generate)
    {
        _status.Text = generate ? "Preparing execution plan…" : "Loading execution plan…";
        try
        {
            var plan = generate
                ? await _api.GenerateExecutionPlanAsync(request.Id)
                : request.ExecutionPlanId.HasValue
                    ? await _api.GetExecutionPlanAsync(request.ExecutionPlanId.Value)
                    : throw new InvalidOperationException("This request has no execution plan.");

            var content = new VerticalStackLayout { Padding = 20, Spacing = 14 };
            var heading = new Label { Text = "Review execution plan", FontSize = 23, FontAttributes = FontAttributes.Bold };
            heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
            content.Children.Add(heading);
            content.Children.Add(new Label { Text = $"Request: {request.Title}", FontSize = 14 });
            var planStatus = new Label { Text = $"Status: {plan.Status}", FontSize = 13 };
            planStatus.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            content.Children.Add(planStatus);

            var switches = new Dictionary<Guid, Switch>();
            foreach (var item in plan.Items)
            {
                var toggle = new Switch { IsToggled = item.IsSelected, HorizontalOptions = LayoutOptions.End };
                var title = new Label { Text = item.Title, FontAttributes = FontAttributes.Bold, FontSize = 15 };
                var type = new Label { Text = item.Type, FontSize = 12 };
                type.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
                var row = new VerticalStackLayout { Spacing = 3 };
                var itemHeader = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Children = { title, toggle }
                };
                Grid.SetColumn(toggle, 1);
                row.Children.Add(itemHeader);
                row.Children.Add(type);
                if (item.ResultingEntityId.HasValue)
                {
                    var result = new Label { Text = $"Created · {item.ResultingEntityId.Value}", FontSize = 11 };
                    result.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
                    row.Children.Add(result);
                }
                var border = new Border { Padding = 12, Content = row };
                border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
                content.Children.Add(border);
                switches[item.Id] = toggle;
                if (plan.Status == "Committed") toggle.IsEnabled = false;
            }

            var message = new Label { FontSize = 13 };
            message.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            content.Children.Add(message);
            var cancel = new Button { Text = plan.Status == "Committed" ? "Close" : "Not now" };
            cancel.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            cancel.Clicked += async (_, _) => await Navigation.PopModalAsync();
            var buttons = new HorizontalStackLayout { Spacing = 10, Children = { cancel } };

            if (plan.Status != "Committed")
            {
                var commit = new Button { Text = "Create selected items" };
                commit.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
                commit.Clicked += async (_, _) =>
                {
                    var deselected = switches.Where(pair => !pair.Value.IsToggled).Select(pair => pair.Key).ToList();
                    if (switches.All(pair => !pair.Value.IsToggled))
                    {
                        message.Text = "Select at least one item to continue.";
                        return;
                    }
                    commit.IsEnabled = false;
                    message.Text = "Creating selected tasks…";
                    try
                    {
                        var committed = await _api.CommitExecutionPlanAsync(plan.Id, deselected);
                        planStatus.Text = $"Status: {committed.Status}";
                        foreach (var pair in switches) pair.Value.IsEnabled = false;
                        message.Text = $"{committed.Items.Count(i => i.ResultingEntityId.HasValue)} task(s) created. Your plan is saved.";
                        commit.IsVisible = false;
                        cancel.Text = "Done";
                    }
                    catch (FamilyApiException ex)
                    {
                        message.Text = ex.Message;
                        commit.IsEnabled = true;
                    }
                    catch (Exception)
                    {
                        message.Text = "Could not create the selected items. Refresh and try again.";
                        commit.IsEnabled = true;
                    }
                };
                buttons.Children.Add(commit);
            }
            else
            {
                message.Text = $"{plan.Items.Count(i => i.ResultingEntityId.HasValue)} task(s) were created from this plan.";
            }

            content.Children.Add(buttons);
            var page = new ContentPage { Title = "Execution plan", Content = new ScrollView { Content = content } };
            await Navigation.PushModalAsync(new NavigationPage(page));
            _status.Text = "Execution plan ready.";
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception ex)
        {
            _status.Text = ex is InvalidOperationException ? ex.Message : "Could not open the execution plan. Refresh and try again.";
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
