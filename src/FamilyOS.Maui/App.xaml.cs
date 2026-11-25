using FamilyOS.Maui.Pages;
using FamilyOS.Maui.Services;
using FamilyOS.Maui.Services.Api;
using FamilyOS.Maui.Services.Offline;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyOS.Maui;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly IAuthSession _session;

    public App(IServiceProvider services, IThemeService theme, IAuthSession session)
    {
        InitializeComponent();
        _services = services;
        _session = session;
        theme.SetPreference(theme.Preference);

        if (session.IsAuthenticated)
        {
            MainPage = services.GetRequiredService<AppShell>();
            _ = services.GetRequiredService<OfflineFlushService>().FlushAsync();
        }
        else
        {
            MainPage = services.GetRequiredService<LoginPage>();
        }
    }

    public void GoToShell()
    {
        MainPage = _services.GetRequiredService<AppShell>();
        // Best-effort: push any queued mutations when session becomes active.
        _ = _services.GetRequiredService<OfflineFlushService>().FlushAsync();
    }

    public void GoToLogin()
    {
        _session.Clear();
        MainPage = _services.GetRequiredService<LoginPage>();
    }
}
