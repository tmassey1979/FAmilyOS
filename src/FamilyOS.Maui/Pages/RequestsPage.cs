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
    private readonly Button _queueButton = new() { Text = "Approval queue" };
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
                AddCard(_showQueue ? "No requests awaiting your review" : "No requests yet",
                    _showQueue ? "New requests requiring your approval will appear here." : "Requests you create will appear here.");
                _status.Text = "You're all caught up.";
            }
            else
            {
                _status.Text = $"{rows.Count} request{(rows.Count == 1 ? "" : "s")}";
                foreach (var request in rows.OrderByDescending(x => x.CreatedAtUtc))
                    AddRequest(request);
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

            var labels = types.Select(x => x.Name).ToArray();
            var selected = await DisplayActionSheetAsync("Choose a request type", "Cancel", null, labels);
            var type = types.FirstOrDefault(x => string.Equals(x.Name, selected, StringComparison.Ordinal));
            if (type is null) return;

            var title = await DisplayPromptAsync("New request", "What do you need?", "Create", "Cancel",
                placeholder: type.Description ?? type.Name, maxLength: 120);
            if (string.IsNullOrWhiteSpace(title)) return;

            var answers = new Dictionary<string, object?>();
            decimal? amount = null;
            foreach (var question in type.Questions.OrderBy(q => ReadInt(q, "sortOrder")))
            {
                var key = ReadString(question, "key");
                var label = ReadString(question, "label");
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(label)) continue;
                var required = ReadBool(question, "required");
                var questionType = ReadInt(question, "type");
                var placeholder = ReadString(question, "placeholder");
                var options = ReadStringArray(question, "options");
                string? value;

                if (questionType == 2 || questionType == 8)
                {
                    var choice = await DisplayActionSheetAsync(label, "Skip", null, "Yes", "No");
                    value = choice == "Yes" ? "true" : choice == "No" ? "false" : null;
                }
                else if (questionType == 3 && options.Length > 0)
                    value = await DisplayActionSheetAsync(label, "Skip", null, options);
                else
                    value = await DisplayPromptAsync(label, placeholder ?? label, "Continue", "Skip",
                        placeholder: placeholder, maxLength: 500,
                        keyboard: questionType == 1 || questionType == 9 ? Keyboard.Numeric : Keyboard.Default);

                if (string.IsNullOrWhiteSpace(value))
                {
                    if (required)
                    {
                        _status.Text = $"{label} is required. The request was not created.";
                        return;
                    }
                    continue;
                }

                if (questionType == 2 || questionType == 8)
                    answers[key] = value == "true";
                else if (questionType == 1 || questionType == 9)
                {
                    if (!decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.CurrentCulture, out var number))
                    {
                        _status.Text = $"{label} must be a valid number.";
                        return;
                    }
                    answers[key] = number;
                    if (questionType == 9 || key.Equals("amount", StringComparison.OrdinalIgnoreCase))
                        amount = number;
                }
                else answers[key] = value.Trim();
            }

            if (answers.TryGetValue("where", out var where) && !answers.ContainsKey("location"))
                answers["location"] = where;
            if (answers.TryGetValue("leave", out var leave) && !answers.ContainsKey("when"))
                answers["when"] = leave;
            if (answers.TryGetValue("item", out var item) && !answers.ContainsKey("items"))
                answers["items"] = item;

            _status.Text = "Creating request…";
            await _api.CreateAsync(type.Code, title.Trim(), answers, amount);
            _showQueue = false;
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Creating the request timed out. Try again.";
        }
        catch (Exception)
        {
            _status.Text = "The request could not be created. Try again.";
        }
        finally
        {
            _createButton.IsEnabled = true;
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

    private void AddRequest(FamilyRequestDto request)
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

        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _items.Children.Add(border);
    }

    private static IReadOnlyList<(string Name, string Label, bool Primary)> ActionsFor(string status, bool queue)
    {
        if (queue && status is "Submitted" or "PendingApproval" or "UnderReview" or "WaitingForInformation")
            return new[] { ("approve", "Approve", true), ("deny", "Deny", false) };
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
