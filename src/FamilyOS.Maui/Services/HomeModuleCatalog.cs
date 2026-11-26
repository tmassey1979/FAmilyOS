namespace FamilyOS.Maui.Services;

/// <summary>Launcher tile for a major FamilyOS module on the Home grid.</summary>
public sealed record HomeModule(
    string Id,
    string Title,
    string Subtitle,
    string Icon,
    string Route,
    string Glyph,
    string TintHex);

/// <summary>Default modules + per-device order (Preferences).</summary>
public static class HomeModuleCatalog
{
    public const string OrderPreferenceKey = "familyos.home.module-order";

    public static IReadOnlyList<HomeModule> Defaults { get; } =
    [
        new("tasks", "Tasks", "Assign & track", "menu_tasks.png", nameof(Pages.TasksPage), "✓", "#DBEAFE"),
        new("calendar", "Calendar", "Events & plans", "menu_calendar.png", nameof(Pages.CalendarPage), "▦", "#E0E7FF"),
        new("requests", "Requests", "Ask & approve", "menu_requests.png", nameof(Pages.RequestsPage), "✉", "#FCE7F3"),
        new("meals", "Meals", "Plan & share", "menu_meals.png", nameof(Pages.MealsPage), "◉", "#D1FAE5"),
        new("chat", "Chat", "Family chat", "menu_chat.png", nameof(Pages.ChatPage), "💬", "#E0F2FE"),
        new("shopping", "Shopping", "Lists & people", "menu_shopping.png", nameof(Pages.ShoppingPage), "🛒", "#FEF3C7"),
        new("map", "Map", "Family locations", "menu_map.png", "//map", "📍", "#FCE7F3"),
        new("notifications", "Alerts", "Stay informed", "menu_notifications.png", nameof(Pages.NotificationsPage), "🔔", "#FEE2E2"),
        new("settings", "Settings", "Preferences", "menu_settings.png", nameof(Pages.SettingsPage), "⚙", "#E2E8F0"),
    ];

    public static List<HomeModule> GetOrdered()
    {
        var byId = Defaults.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var saved = Preferences.Default.Get(OrderPreferenceKey, string.Empty);
        var result = new List<HomeModule>();

        if (!string.IsNullOrWhiteSpace(saved))
        {
            foreach (var id in saved.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (byId.Remove(id, out var module))
                    result.Add(module);
            }
        }

        foreach (var module in Defaults)
        {
            if (byId.Remove(module.Id))
                result.Add(module);
        }

        return result;
    }

    public static void SaveOrder(IEnumerable<HomeModule> modules)
    {
        Preferences.Default.Set(OrderPreferenceKey, string.Join(',', modules.Select(m => m.Id)));
    }

    public static void Move(List<HomeModule> modules, int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= modules.Count || target < 0 || target >= modules.Count)
            return;
        (modules[index], modules[target]) = (modules[target], modules[index]);
        SaveOrder(modules);
    }
}
