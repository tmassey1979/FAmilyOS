using FamilyOS.Maui.Pages;
using FamilyOS.Maui.Services;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // System fonts; add brand fonts under Resources/Fonts when available.
            });

        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<IAuthSessionService, KeycloakAuthService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddSingleton<PulseApiClient>();
        builder.Services.AddSingleton<TasksApiClient>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddSingleton<CalendarApiClient>();
        builder.Services.AddTransient<CalendarPage>();
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

        return builder.Build();
    }
}
