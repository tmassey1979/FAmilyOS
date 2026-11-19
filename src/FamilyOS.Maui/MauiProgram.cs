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
        builder.Services.AddTransient<DesignSystemGalleryPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
