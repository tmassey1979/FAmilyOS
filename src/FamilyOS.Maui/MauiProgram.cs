using FamilyOS.Maui.Pages;
using FamilyOS.Maui.Services;
using FamilyOS.Maui.Services.Offline;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        var diagnostics = new StartupDiagnosticsService();
        diagnostics.RecordCheckpoint("MauiProgram.CreateMauiApp entered");

        builder.Services.AddSingleton(diagnostics);

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IThemeService, ThemeService>();

        // Shared HTTP + auth session used by all API clients.
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<IAuthSessionService, KeycloakAuthService>();
        builder.Services.AddSingleton<ClientLogService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddSingleton<PulseApiClient>();
        builder.Services.AddSingleton<TasksApiClient>();
        builder.Services.AddSingleton<RequestsApiClient>();
        builder.Services.AddSingleton<NotificationsApiClient>();
        builder.Services.AddSingleton<MealsApiClient>();
        builder.Services.AddSingleton<ChatApiClient>();
        builder.Services.AddSingleton<OutcomesApiClient>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddSingleton<CalendarApiClient>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddSingleton<OnboardingApiClient>();
        // Demo-only provider behind a purchase abstraction; replace with Google Play Billing later.
        builder.Services.AddSingleton<ISubscriptionPurchaseService, DemoSubscriptionPurchaseService>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddSingleton<OfflineStore>();
        builder.Services.AddSingleton<OfflineFlushService>();
        builder.Services.AddSingleton<ConnectivityMonitor>();
        builder.Services.AddSingleton<FamilyApiClient>();
        builder.Services.AddSingleton<DeviceLocationService>();
        builder.Services.AddTransient<LocationPage>();
        builder.Services.AddTransient<CheckInsPage>();
        builder.Services.AddTransient<EmergencySosPage>();
        builder.Services.AddTransient<FamilyMapPage>();
        builder.Services.AddTransient<SavedPlacesPage>();
        builder.Services.AddSingleton<MeApiClient>();
        builder.Services.AddTransient<FamilyPage>();
        builder.Services.AddTransient<DesignSystemGalleryPage>();
        builder.Services.AddTransient<RequestsPage>();
        builder.Services.AddTransient<MealsPage>();
        builder.Services.AddTransient<RecipeEditorPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<OutcomesPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        try
        {
            diagnostics.RecordCheckpoint("MauiProgram.Build starting");
            var app = builder.Build();
            diagnostics.RecordCheckpoint("MauiProgram.Build completed");

            // Resolve once so the flush timer starts; log launch for remote observability.
            var clientLogs = app.Services.GetService<ClientLogService>();
            clientLogs?.Info("app_launch", "MauiProgram");
            app.Services.GetService<ConnectivityMonitor>()?.Start();

            return app;
        }
        catch (Exception ex)
        {
            diagnostics.RecordException("MauiProgram.Build failed", ex);
            throw;
        }
    }
}
