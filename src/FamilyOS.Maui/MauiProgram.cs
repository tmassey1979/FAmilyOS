using FamilyOS.Maui.Pages;
using FamilyOS.Maui.Services;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var diagnostics = new StartupDiagnosticsService();
        diagnostics.BeginLaunch();

        var builder = MauiApp.CreateBuilder();
        builder.Services.AddSingleton(diagnostics);
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // System fonts; add brand fonts under Resources/Fonts when available.
            });

        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddTransient<StartupDiagnosticsPage>();
        builder.Services.AddTransient<AccessTokenPage>();
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
        builder.Services.AddTransient<OnboardingPage>();
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
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<OutcomesPage>();
        builder.Services.AddTransient<NotificationsPage>();

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

            return app;
        }
        catch (Exception ex)
        {
            diagnostics.RecordException("MauiProgram.Build failed", ex);
            throw;
        }
    }
}
