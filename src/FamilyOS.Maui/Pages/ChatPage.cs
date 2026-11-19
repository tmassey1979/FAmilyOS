using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Family-wide chat backed by the authenticated chat API.</summary>
public sealed class ChatPage : ContentPage
{
    private readonly ChatApiClient _api;
    private readonly VerticalStackLayout _messages = new() { Spacing = 10 };
    private readonly Label _status = new() { FontSize = 12 };
    private readonly Entry _composer = new() { Placeholder = "Message your family…", MaxLength = 2000, ReturnType = ReturnType.Send };
    private readonly Button _send = new() { Text = "Send" };
    private readonly Button _refresh = new() { Text = "Refresh" };
    private readonly Button _older = new() { Text = "Load older messages" };
    private readonly ScrollView _scroll = new() { VerticalOptions = LayoutOptions.FillAndExpand };
    private ChatChannelDto? _channel;
    private DateTime? _oldestMessage;
    private bool _loading;
    private bool _sending;

    public ChatPage(ChatApiClient api)
    {
        _api = api;
        Title = "Chat";
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _send.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _older.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();
        _older.Clicked += async (_, _) => await LoadOlderAsync();
        _send.Clicked += async (_, _) => await SendAsync();
        _composer.Completed += async (_, _) => await SendAsync();

        _scroll.Content = _messages;
        var heading = new Label { Text = "Family chat", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var toolbar = new HorizontalStackLayout { Spacing = 8, Children = { _refresh, _older } };
        var composer = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
            Children = { _composer, _send }
        };
        Grid.SetColumn(_send, 1);
        Content = new Grid
        {
            Padding = new Thickness(16, 12),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 10,
            Children = { heading, toolbar, _status, _scroll, composer }
        };
        Grid.SetRow(toolbar, 1);
        Grid.SetRow(_status, 2);
        Grid.SetRow(_scroll, 3);
        Grid.SetRow(composer, 4);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_channel is null && !_loading) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        SetBusy(true);
        _status.Text = "Loading family chat…";
        try
        {
            _channel ??= await _api.GetChannelAsync();
            var rows = await _api.GetMessagesAsync(_channel.Id, 50);
            _messages.Children.Clear();
            foreach (var item in rows) AddMessage(item);
            _oldestMessage = rows.Count == 0 ? null : rows[0].SentAtUtc;
            _older.IsEnabled = rows.Count == 50;
            _status.Text = $"{_channel.Name} · {rows.Count} recent message(s)";
            await ScrollToLatestAsync();
        }
        catch (OperationCanceledException)
        {
            _status.Text = "The chat request timed out. Refresh and try again.";
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
            _status.Text = "Something went wrong while loading family chat.";
        }
        finally
        {
            _loading = false;
            SetBusy(false);
        }
    }

    private async Task LoadOlderAsync()
    {
        if (_loading || _channel is null || _oldestMessage is null) return;
        _loading = true;
        _older.IsEnabled = false;
        try
        {
            var rows = await _api.GetMessagesAsync(_channel.Id, 50, _oldestMessage);
            if (rows.Count == 0)
            {
                _status.Text = "No older messages.";
                return;
            }
            foreach (var item in rows) _messages.Children.Insert(0, BuildMessage(item));
            _oldestMessage = rows[0].SentAtUtc;
            _older.IsEnabled = rows.Count == 50;
            _status.Text = $"Loaded {rows.Count} older message(s).";
        }
        catch (Exception ex)
        {
            _status.Text = ex is FamilyApiException ? ex.Message : "Could not load older messages.";
        }
        finally
        {
            _loading = false;
            _older.IsEnabled = true;
        }
    }

    private async Task SendAsync()
    {
        var body = _composer.Text?.Trim();
        if (string.IsNullOrWhiteSpace(body) || _sending) return;
        _sending = true;
        _send.IsEnabled = false;
        _composer.IsEnabled = false;
        try
        {
            _channel ??= await _api.GetChannelAsync();
            var sent = await _api.SendAsync(body, _channel.Id);
            _composer.Text = string.Empty;
            if (!_messages.Children.OfType<Border>().Any())
                AddMessage(sent);
            else
                AddMessage(sent);
            _status.Text = $"Sent · {sent.SentAtUtc.ToLocalTime():t}";
            await ScrollToLatestAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Message not sent",
                ex is FamilyApiException ? ex.Message : "Could not send your message. Check your connection and try again.", "OK");
        }
        finally
        {
            _sending = false;
            _send.IsEnabled = true;
            _composer.IsEnabled = true;
        }
    }

    private void AddMessage(ChatMessageDto item) => _messages.Children.Add(BuildMessage(item));

    private static Border BuildMessage(ChatMessageDto item)
    {
        var mine = false;
        var title = new Label { Text = item.SenderName, FontSize = 12, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        var body = new Label { Text = item.Body, FontSize = 15, LineBreakMode = LineBreakMode.WordWrap };
        body.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        var time = new Label { Text = item.SentAtUtc.ToLocalTime().ToString("g"), FontSize = 11, HorizontalTextAlignment = TextAlignment.End };
        time.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        var stack = new VerticalStackLayout { Spacing = 5, Children = { title, body, time } };
        var card = new Border { Padding = 12, Content = stack, HorizontalOptions = LayoutOptions.Fill };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return card;
    }

    private async Task ScrollToLatestAsync()
    {
        await Task.Delay(30);
        if (_messages.Children.LastOrDefault() is Element last)
            await _scroll.ScrollToAsync(last, ScrollToPosition.End, false);
    }

    private void SetBusy(bool busy)
    {
        _refresh.IsEnabled = !busy;
        _older.IsEnabled = !busy && _oldestMessage is not null;
    }
}
