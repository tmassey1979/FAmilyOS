using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

public partial class DesignSystemGalleryPage : ContentPage
{
    private readonly IThemeService _theme;

    public DesignSystemGalleryPage(IThemeService theme)
    {
        _theme = theme;
        InitializeComponent();
    }

    private void OnThemeSystem(object? sender, EventArgs e) =>
        _theme.SetPreference(AppThemePreference.System);

    private void OnThemeLight(object? sender, EventArgs e) =>
        _theme.SetPreference(AppThemePreference.Light);

    private void OnThemeDark(object? sender, EventArgs e) =>
        _theme.SetPreference(AppThemePreference.Dark);
}
