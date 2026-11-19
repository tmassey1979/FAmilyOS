using FamilyOS.Maui.Pages;

namespace FamilyOS.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(RequestsPage), typeof(RequestsPage));
        Routing.RegisterRoute(nameof(MealsPage), typeof(MealsPage));
        Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(OutcomesPage), typeof(OutcomesPage));
        Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
        Routing.RegisterRoute(nameof(DesignSystemGalleryPage), typeof(DesignSystemGalleryPage));
    }
}
