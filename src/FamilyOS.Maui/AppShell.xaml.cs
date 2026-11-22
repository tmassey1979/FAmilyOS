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
        TasksShellContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<TasksPage>());
        CalendarShellContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<CalendarPage>());
        FamilyShellContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<FamilyPage>());

        Routing.RegisterRoute(nameof(OnboardingPage), typeof(OnboardingPage));
        Routing.RegisterRoute(nameof(RequestsPage), typeof(RequestsPage));
        Routing.RegisterRoute(nameof(MealsPage), typeof(MealsPage));
        Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(OutcomesPage), typeof(OutcomesPage));
        Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
        Routing.RegisterRoute(nameof(LocationPage), typeof(LocationPage));
        Routing.RegisterRoute(nameof(FamilyMapPage), typeof(FamilyMapPage));
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
