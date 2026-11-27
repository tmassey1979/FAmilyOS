using FamilyOS.Maui.Controls;
using FamilyOS.Maui.Services.Api;
using FamilyOS.Maui.Services.Offline;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyOS.Maui.Pages;

/// <summary>Secondary destinations hub — settings-style sci-fi rows.</summary>
public sealed class MorePage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly IAuthSession _session;
    private readonly OfflineStore _offline;
    private readonly OfflineFlushService _flush;
    private readonly Label _offlineStatus;

    public MorePage(
        IServiceProvider services,
        IAuthSession session,
        OfflineStore offline,
        OfflineFlushService flush)
    {
        _services = services;
        _session = session;
        _offline = offline;
        _flush = flush;
        Title = "More";

        _offlineStatus = Ui.Caption("…");
        RefreshOfflineStatus(_offline.GetQueue().Count);
        _offline.SubscribeQueueCount(count =>
            MainThread.BeginInvokeOnMainThread(() => RefreshOfflineStatus(count)));

        var syncBtn = Ui.SecondaryCta("Sync offline queue");
        syncBtn.Clicked += async (_, _) =>
        {
            try
            {
                var n = await _flush.FlushAsync();
                _offlineStatus.Text = n > 0
                    ? $"Synced {n} change{(n == 1 ? "" : "s")}."
                    : (_offline.GetQueue().Count == 0
                        ? "Nothing waiting to sync."
                        : "Still offline or server unreachable — queue kept.");
            }
            catch (Exception ex)
            {
                _offlineStatus.Text = ex.Message;
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12, 16, 28),
                Spacing = 10,
                Children =
                {
                    Ui.PageHeader("More", "Household tools & system"),
                    Ui.Section("Household"),
                    Nav("◈  Family", "Members, roles, invites", typeof(FamilyPage)),
                    Nav("◉  Notifications", "Inbox and read state", typeof(NotificationsPage)),
                    Ui.Section("Daily ops"),
                    Nav("◎  Meals", "Weekly plan → shopping", typeof(MealsPage)),
                    Nav("✦  Recipes", "Favorites → plan or list", typeof(RecipesPage)),
                    Nav("▣  Shopping", "Queue, carts, purchase", typeof(ShoppingPage)),
                    Ui.Section("System"),
                    Nav("⬡  Design system", "Fo* control gallery", typeof(DesignSystemGalleryPage)),
                    Ui.Section("Offline"),
                    Ui.Card(new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children = { _offlineStatus, syncBtn }
                    }),
                    SignOutBlock()
                }
            }
        };
    }

    private void RefreshOfflineStatus(int queueCount)
    {
        var online = _offline.IsOnline ? "● Online" : "○ Offline";
        _offlineStatus.Text = queueCount == 0
            ? $"{online}  ·  queue empty"
            : $"{online}  ·  {queueCount} change{(queueCount == 1 ? "" : "s")} waiting";
    }

    private View Nav(string title, string subtitle, Type pageType) =>
        Ui.NavRow(title, subtitle, async (_, _) =>
        {
            if (_services.GetService(pageType) is not Page page)
                return;
            if (Shell.Current?.Navigation is { } nav)
                await nav.PushAsync(page);
            else
                await Navigation.PushAsync(page);
        });

    private View SignOutBlock()
    {
        var btn = Ui.DangerAction("Sign out");
        btn.HorizontalOptions = LayoutOptions.Fill;
        btn.MinimumHeightRequest = 48;
        btn.Margin = new Thickness(0, 8, 0, 0);
        btn.Clicked += (_, _) =>
        {
            if (Application.Current is App app)
                app.GoToLogin();
        };

        var meta = Ui.Caption($"API  {_session.BaseUrl}");
        meta.LineBreakMode = LineBreakMode.MiddleTruncation;

        return new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 24),
            Children = { btn, meta }
        };
    }
}
