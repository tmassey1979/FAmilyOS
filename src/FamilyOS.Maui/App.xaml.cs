using FamilyOS.Maui.Pages;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui;

public partial class App : Application
{
    public App(IThemeService theme, DesignSystemGalleryPage gallery)
    {
        InitializeComponent();
        theme.SetPreference(theme.Preference);
        MainPage = new NavigationPage(gallery);
    }
}
