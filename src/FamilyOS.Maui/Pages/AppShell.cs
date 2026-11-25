namespace FamilyOS.Maui.Pages;

/// <summary>
/// Primary tabs for core journeys. Secondary screens live under More.
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
