namespace FamilyOS.Maui.Pages;

/// <summary>
/// Primary tabs for core journeys. Secondary screens live under More.
/// Sci-fi chrome: cyan selected tabs on deep surface.
/// </summary>
public sealed class AppShell : Shell
{
    public AppShell(
        PulsePage pulse,
        TasksPage tasks,
        RequestsPage requests,
        CalendarPage calendar,
        ChatPage chat,
        MorePage more)
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;

        // Tab bar + chrome colors (also in Styles.xaml; set explicitly for reliability)
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
                new ShellContent { Title = "Pulse", Route = "pulse", Content = pulse },
                new ShellContent { Title = "Tasks", Route = "tasks", Content = tasks },
                new ShellContent { Title = "Requests", Route = "requests", Content = requests },
                new ShellContent { Title = "Calendar", Route = "calendar", Content = calendar },
                new ShellContent { Title = "Chat", Route = "chat", Content = chat },
                new ShellContent { Title = "More", Route = "more", Content = more },
            }
        });
    }
}
