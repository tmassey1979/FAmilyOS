using FamilyOS.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyOS.Maui;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly IThemeService _theme;
    private readonly StartupDiagnosticsService _diagnostics;

    public App(IServiceProvider services, StartupDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        _diagnostics.RecordCheckpoint("App constructor entered");

        try
        {
            // Load Application resources before constructing AppShell. The Shell XAML
            // references StaticResource keys defined in App.xaml, so resolving Shell
            // through constructor injection before InitializeComponent can crash startup.
            InitializeComponent();
            _diagnostics.RecordCheckpoint("App.InitializeComponent completed");

            _theme = services.GetRequiredService<IThemeService>();
            _diagnostics.RecordCheckpoint("App theme service resolved");

            _shell = services.GetRequiredService<AppShell>();
            _diagnostics.RecordCheckpoint("AppShell service resolved");

            // Apply stored preference once Application.Current and its resources exist.
            _theme.SetPreference(_theme.Preference);
            _diagnostics.RecordCheckpoint("App theme preference applied");
        }
        catch (Exception ex)
        {
            _diagnostics.RecordException("App initialization failed", ex);
            throw;
        }
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
