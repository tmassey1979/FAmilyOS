using FamilyOS.Maui.Pages;
using FamilyOS.Maui.Services;
using FamilyOS.Maui.Services.Api;
using FamilyOS.Maui.Services.Offline;
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
        builder.Services.AddSingleton<IAuthSession, AuthSession>();
        builder.Services.AddSingleton<OfflineStore>();

        builder.Services.AddHttpClient<FamilyHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddHttpClient(nameof(OfflineFlushService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<OfflineFlushService>();

        builder.Services.AddSingleton<MeApiClient>();
        builder.Services.AddSingleton<FamilyApiClient>();
        builder.Services.AddSingleton<PulseApiClient>();
        builder.Services.AddSingleton<TasksApiClient>();
        builder.Services.AddSingleton<RequestsApiClient>();
        builder.Services.AddSingleton<CalendarApiClient>();
        builder.Services.AddSingleton<ChatApiClient>();
        builder.Services.AddSingleton<ChatHubService>();
        builder.Services.AddSingleton<NotificationsApiClient>();
        builder.Services.AddSingleton<MealsApiClient>();
        builder.Services.AddSingleton<RecipesApiClient>();
        builder.Services.AddSingleton<ProcurementApiClient>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<PulsePage>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddTransient<RequestsPage>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<MorePage>();
        builder.Services.AddTransient<FamilyPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<MealsPage>();
        builder.Services.AddTransient<RecipesPage>();
        builder.Services.AddTransient<ShoppingPage>();
        builder.Services.AddTransient<DesignSystemGalleryPage>();
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
