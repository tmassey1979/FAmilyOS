using FamilyOS.Maui.Pages;
using FamilyOS.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyOS.Maui;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services, StartupDiagnosticsService diagnostics)
    {
        diagnostics.RecordCheckpoint("AppShell constructor entered");
        InitializeComponent();
        diagnostics.RecordCheckpoint("AppShell.InitializeComponent completed");

        HomeShellContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>());
        FamilyShellContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<FamilyPage>());
        MapShellContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<FamilyMapPage>());

        // Modules opened from the Home grid (not bottom tabs).
        Routing.RegisterRoute(nameof(TasksPage), typeof(TasksPage));
        Routing.RegisterRoute(nameof(CalendarPage), typeof(CalendarPage));
        Routing.RegisterRoute(nameof(OnboardingPage), typeof(OnboardingPage));
        Routing.RegisterRoute(nameof(RequestsPage), typeof(RequestsPage));
        Routing.RegisterRoute(nameof(MealsPage), typeof(MealsPage));
        Routing.RegisterRoute(nameof(ShoppingPage), typeof(ShoppingPage));
        Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(OutcomesPage), typeof(OutcomesPage));
        Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
        Routing.RegisterRoute(nameof(LocationPage), typeof(LocationPage));
        Routing.RegisterRoute(nameof(CheckInsPage), typeof(CheckInsPage));
        Routing.RegisterRoute(nameof(EmergencySosPage), typeof(EmergencySosPage));
        Routing.RegisterRoute(nameof(SavedPlacesPage), typeof(SavedPlacesPage));
        Routing.RegisterRoute(nameof(DesignSystemGalleryPage), typeof(DesignSystemGalleryPage));
        Routing.RegisterRoute(nameof(StartupDiagnosticsPage), typeof(StartupDiagnosticsPage));
        Routing.RegisterRoute(nameof(AccessTokenPage), typeof(AccessTokenPage));

        Navigated += (_, _) =>
        {
            diagnostics.RecordCheckpoint("AppShell navigation completed");
            diagnostics.MarkLaunchCompleted();
        };
    }
}
