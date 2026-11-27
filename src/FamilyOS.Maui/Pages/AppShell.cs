namespace FamilyOS.Maui.Pages;

/// <summary>
/// Primary tabs for core journeys. Secondary screens live under Settings/More routes.
/// Sci-fi chrome: cyan selected tabs on deep surface.
/// </summary>
public sealed class AppShell : Shell
{
    public AppShell(
        HomePage home,
        TasksPage tasks,
        RequestsPage requests,
        CalendarPage calendar,
        ChatPage chat,
        SettingsPage settings)
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;

        SetDynamicResource(BackgroundColorProperty, "BgPage");
        SetDynamicResource(Shell.TabBarBackgroundColorProperty, "BgSurface");
        SetDynamicResource(Shell.TabBarForegroundColorProperty, "AccentPrimary");
        SetDynamicResource(Shell.TabBarTitleColorProperty, "AccentGlow");
        SetDynamicResource(Shell.TabBarUnselectedColorProperty, "TextMuted");
        SetDynamicResource(Shell.ForegroundColorProperty, "AccentPrimary");
        SetDynamicResource(Shell.TitleColorProperty, "TextPrimary");

        Items.Add(new TabBar
        {
            Items =
            {
                new ShellContent { Title = "Home", Route = "home", Content = home },
                new ShellContent { Title = "Tasks", Route = "tasks", Content = tasks },
                new ShellContent { Title = "Requests", Route = "requests", Content = requests },
                new ShellContent { Title = "Calendar", Route = "calendar", Content = calendar },
                new ShellContent { Title = "Chat", Route = "chat", Content = chat },
                new ShellContent { Title = "Settings", Route = "settings", Content = settings },
            }
        });
    }
}
