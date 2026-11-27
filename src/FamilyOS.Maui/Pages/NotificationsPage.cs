using FamilyOS.Maui.Controls;
using FamilyOS.Maui.Models;
using FamilyOS.Maui.Services.Api;

namespace FamilyOS.Maui.Pages;

public sealed class NotificationsPage : ContentPage
{
    private readonly NotificationsApiClient _notifications;
    private readonly VerticalStackLayout _items;
    private readonly Label _status;
    private bool _loading;

    public NotificationsPage(NotificationsApiClient notifications)
    {
        _notifications = notifications;
        Title = "Notifications";

        _status = Ui.Caption("");
        _items = new VerticalStackLayout { Spacing = 10 };

        var refresh = Ui.Ghost("↻");
        refresh.Clicked += async (_, _) => await LoadAsync();

        var markAll = Ui.Action("Mark all read");
        markAll.Clicked += async (_, _) =>
        {
            try
            {
                await _notifications.MarkAllReadAsync();
                await LoadAsync();
            }
            catch (FamilyApiException ex) { _status.Text = ex.Message; }
        };

        Content = Ui.PageScroll(
            Ui.PageHeader("Notifications", "Inbox", markAll, refresh),
            _status,
            _items);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _status.Text = "Loading…";
        _items.Children.Clear();
        try
        {
            var list = await _notifications.MineAsync();
            if (list.Count == 0)
            {
                _items.Children.Add(Ui.Empty("Inbox empty", "You are all caught up."));
                _status.Text = "Inbox empty";
            }
            else
            {
                var unread = list.Count(n => !n.IsRead);
                _status.Text = unread > 0 ? $"{unread} unread · {list.Count} total" : $"{list.Count} notifications";
                foreach (var n in list.OrderByDescending(x => x.CreatedAtUtc).Take(40))
                {
                    var stack = new VerticalStackLayout
                    {
                        Spacing = 6,
                        Opacity = n.IsRead ? 0.7 : 1,
                        Children =
                        {
                            Ui.Heading(n.Title),
                            Ui.Body(n.Body),
                            Ui.Caption($"{n.Type}  ·  {n.CreatedAtUtc.ToLocalTime():g}")
                        }
                    };

                    if (!n.IsRead)
                    {
                        var mark = Ui.Action("Mark read");
                        mark.Clicked += async (_, _) =>
                        {
                            try
                            {
                                await _notifications.MarkReadAsync(n.Id);
                                await LoadAsync();
                            }
                            catch (FamilyApiException ex) { _status.Text = ex.Message; }
                        };
                        stack.Children.Add(mark);
                    }

                    _items.Children.Add(Ui.ListCard(stack, railTone: n.IsRead ? "muted" : "info", accent: !n.IsRead));
                }
            }
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not load notifications.";
        }
        finally
        {
            _loading = false;
        }
    }
}
