namespace FamilyOS.Maui.Services;

/// <summary>
/// Runtime theme preference. Updates Application.UserAppTheme and overlays semantic
/// color keys so pages using {StaticResource BgPage} etc. reflect the active theme.
/// </summary>
public enum AppThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2
}

public interface IThemeService
{
    AppThemePreference Preference { get; }
    void SetPreference(AppThemePreference preference);
    event EventHandler? ThemeChanged;
}

public sealed class ThemeService : IThemeService
{
    public const string PreferenceKey = "familyos.theme.preference";

    private static readonly Dictionary<string, Color> Light = new(StringComparer.Ordinal)
    {
        ["BgPage"] = Color.FromArgb("#F4F7FC"),
        ["BgSurface"] = Color.FromArgb("#FFFFFF"),
        ["BgSurfaceAlt"] = Color.FromArgb("#EAF0F8"),
        ["BgGlow"] = Color.FromArgb("#E5F8FF"),
        ["BorderSubtle"] = Color.FromArgb("#D7E1EF"),
        ["TextPrimary"] = Color.FromArgb("#101A2B"),
        ["TextMuted"] = Color.FromArgb("#60718A"),
        ["TextOnAccent"] = Color.FromArgb("#081321"),
        ["AccentPrimary"] = Color.FromArgb("#008DA8"),
        ["AccentSecondary"] = Color.FromArgb("#6D5CE7"),
        ["AccentDanger"] = Color.FromArgb("#D83D5B"),
        ["AccentSuccess"] = Color.FromArgb("#087F61"),
        ["AccentWarning"] = Color.FromArgb("#956000"),
        ["OverlayScrim"] = Color.FromArgb("#00000066"),
    };

    private static readonly Dictionary<string, Color> Dark = new(StringComparer.Ordinal)
    {
        ["BgPage"] = Color.FromArgb("#030712"),
        ["BgSurface"] = Color.FromArgb("#0A1020"),
        ["BgSurfaceAlt"] = Color.FromArgb("#0E1A2E"),
        ["BgGlow"] = Color.FromArgb("#071A2C"),
        ["BorderSubtle"] = Color.FromArgb("#1B3552"),
        ["TextPrimary"] = Color.FromArgb("#EAF6FF"),
        ["TextMuted"] = Color.FromArgb("#8CA7C5"),
        ["TextOnAccent"] = Color.FromArgb("#04101A"),
        ["AccentPrimary"] = Color.FromArgb("#45F3FF"),
        ["AccentSecondary"] = Color.FromArgb("#B18CFF"),
        ["AccentDanger"] = Color.FromArgb("#FF5D7D"),
        ["AccentSuccess"] = Color.FromArgb("#47F0B2"),
        ["AccentWarning"] = Color.FromArgb("#FFC66D"),
        ["OverlayScrim"] = Color.FromArgb("#000000B3"),
    };

    public AppThemePreference Preference { get; private set; }

    public event EventHandler? ThemeChanged;

    public ThemeService()
    {
        var stored = Preferences.Default.Get(PreferenceKey, nameof(AppThemePreference.Dark));
        Preference = Enum.TryParse<AppThemePreference>(stored, out var p)
            ? p
            : AppThemePreference.System;
        Apply(Preference);
    }

    public void SetPreference(AppThemePreference preference)
    {
        Preference = preference;
        Preferences.Default.Set(PreferenceKey, preference.ToString());
        Apply(preference);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Apply(AppThemePreference preference)
    {
        if (Application.Current is null)
            return;

        Application.Current.UserAppTheme = preference switch
        {
            AppThemePreference.Light => AppTheme.Light,
            AppThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        var useDark = preference switch
        {
            AppThemePreference.Light => false,
            AppThemePreference.Dark => true,
            _ => Application.Current.RequestedTheme == AppTheme.Dark
        };

        var map = useDark ? Dark : Light;
        foreach (var (key, color) in map)
            Application.Current.Resources[key] = color;
    }
}
