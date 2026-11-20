using FamilyOS.Maui.Services;

namespace FamilyOS.Maui;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly IThemeService _theme;
    private readonly StartupDiagnosticsService _diagnostics;

    public App(IThemeService theme, AppShell shell, StartupDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        _diagnostics.RecordCheckpoint("App constructor entered");
        InitializeComponent();
        _diagnostics.RecordCheckpoint("App.InitializeComponent completed");
        _theme = theme;
        _shell = shell;
        // Apply stored preference once Application.Current exists.
        _theme.SetPreference(_theme.Preference);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _diagnostics.RecordCheckpoint("App.CreateWindow entered");
        try
        {
            var window = new Window(_shell);
            _diagnostics.RecordCheckpoint("App.CreateWindow completed");
            return window;
        }
        catch (Exception ex)
        {
            _diagnostics.RecordException("App.CreateWindow failed", ex);
            throw;
        }
    }
}
