using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>API-backed household notification inbox.</summary>
public sealed class NotificationsPage : ContentPage
{
    private readonly NotificationsApiClient _api;
    private readonly VerticalStackLayout _items = new() { Spacing = 10 };
    private readonly Label _status = new() { FontSize = 13 };
    private readonly Button _refresh = new() { Text = "Refresh" };
    private readonly Button _markAll = new() { Text = "Mark all read" };
    private bool _loading;

    public NotificationsPage(NotificationsApiClient api)
    {
        _api = api;
        Title = "Notifications";
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _markAll.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();
        _markAll.Clicked += async (_, _) => await MarkAllReadAsync();

        var heading = new Label { Text = "Notifications", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var actions = new HorizontalStackLayout { Spacing = 8, Children = { _refresh, _markAll } };
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12),
            Spacing = 16,
            Children = { heading, actions, _status, _items }
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
        _refresh.IsEnabled = false;
        _markAll.IsEnabled = false;
        _status.Text = "Loading notifications…";
        try
        {
            var rows = await _api.GetMineAsync();
            _items.Children.Clear();
            var unread = rows.Count(x => !x.IsRead);
            _status.Text = $"{unread} unread · {rows.Count} recent";
            _markAll.IsEnabled = unread > 0;
            if (rows.Count == 0)
                AddCard("You're all caught up", "Family updates and reminders will appear here.");
            else
                foreach (var item in rows.OrderByDescending(x => x.CreatedAtUtc))
                    AddNotification(item);
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
            _status.Text = "Something went wrong while loading notifications.";
        }
        finally
        {
            _loading = false;
            _refresh.IsEnabled = true;
            _markAll.IsEnabled = true;
        }
    }

    private void AddNotification(FamilyNotificationDto item)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        var title = new Label { Text = item.Title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(title);
        var body = new Label { Text = item.Body, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
        body.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        stack.Children.Add(body);
        var meta = new Label
        {
            Text = $"{item.Type} · {item.CreatedAtUtc.ToLocalTime():g}" + (item.IsRead ? "" : " · UNREAD"),
            FontSize = 12
        };
        meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        stack.Children.Add(meta);

        if (!item.IsRead)
        {
            var button = new Button { Text = "Mark read", FontSize = 13, HorizontalOptions = LayoutOptions.Start };
            button.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
            button.Clicked += async (_, _) => await MarkReadAsync(item.Id);
            stack.Children.Add(button);
        }

        var border = new Border { Padding = 14, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        if (!item.IsRead)
            border.Stroke = (Color)Application.Current!.Resources["AccentPrimary"];
        _items.Children.Add(border);
    }

    private async Task MarkReadAsync(Guid id)
    {
        try
        {
            await _api.MarkReadAsync(id);
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not mark that notification as read.";
        }
    }

    private async Task MarkAllReadAsync()
    {
        _markAll.IsEnabled = false;
        try
        {
            await _api.MarkAllReadAsync();
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not mark notifications as read.";
        }
        finally
        {
            _markAll.IsEnabled = true;
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
