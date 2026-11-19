using FamilyOS.Maui.Services;

namespace FamilyOS.Maui;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly IThemeService _theme;

    public App(IThemeService theme, AppShell shell)
    {
        InitializeComponent();
        _theme = theme;
        _shell = shell;
        // Apply stored preference once Application.Current exists.
        _theme.SetPreference(_theme.Preference);
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(_shell);
}
