using FamilyOS.Maui.Services;

namespace FamilyOS.Maui;

public partial class App : Application
{
    public App(IThemeService theme, AppShell shell)
    {
        InitializeComponent();
        theme.SetPreference(theme.Preference);
        MainPage = shell;
    }
}
