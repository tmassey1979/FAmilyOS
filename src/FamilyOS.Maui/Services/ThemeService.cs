namespace FamilyOS.Maui.Services;

/// <summary>
/// Runtime theme preference. Updates Application.UserAppTheme and overlays semantic
/// color keys so pages using {StaticResource BgPage} etc. reflect the active theme.
/// Sci-fi / command-center palette: deep space + electric cyan.
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
        ["BgPage"] = Color.FromArgb("#EEF2F7"),
        ["BgSurface"] = Color.FromArgb("#FFFFFF"),
        ["BgSurfaceAlt"] = Color.FromArgb("#E2E8F0"),
        ["BgGlow"] = Color.FromArgb("#E0F2FE"),
        ["BorderSubtle"] = Color.FromArgb("#CBD5E1"),
        ["BorderAccent"] = Color.FromArgb("#22D3EE55"),
        ["TextPrimary"] = Color.FromArgb("#0B1220"),
        ["TextMuted"] = Color.FromArgb("#64748B"),
        ["TextOnAccent"] = Color.FromArgb("#041018"),
        ["AccentPrimary"] = Color.FromArgb("#0891B2"),
        ["AccentSecondary"] = Color.FromArgb("#7C3AED"),
        ["AccentGlow"] = Color.FromArgb("#22D3EE"),
        ["AccentDanger"] = Color.FromArgb("#E11D48"),
        ["AccentSuccess"] = Color.FromArgb("#059669"),
        ["AccentWarning"] = Color.FromArgb("#D97706"),
        ["OverlayScrim"] = Color.FromArgb("#00000066"),
    };

    private static readonly Dictionary<string, Color> Dark = new(StringComparer.Ordinal)
    {
        ["BgPage"] = Color.FromArgb("#0A0E14"),
        ["BgSurface"] = Color.FromArgb("#121A2B"),
        ["BgSurfaceAlt"] = Color.FromArgb("#1A2438"),
        ["BgGlow"] = Color.FromArgb("#152033"),
        ["BorderSubtle"] = Color.FromArgb("#2A3A55"),
        ["BorderAccent"] = Color.FromArgb("#22D3EE40"),
        ["TextPrimary"] = Color.FromArgb("#F1F5F9"),
        ["TextMuted"] = Color.FromArgb("#94A3B8"),
        ["TextOnAccent"] = Color.FromArgb("#041018"),
        ["AccentPrimary"] = Color.FromArgb("#22D3EE"),
        ["AccentSecondary"] = Color.FromArgb("#A78BFA"),
        ["AccentGlow"] = Color.FromArgb("#67E8F9"),
        ["AccentDanger"] = Color.FromArgb("#FB7185"),
        ["AccentSuccess"] = Color.FromArgb("#34D399"),
        ["AccentWarning"] = Color.FromArgb("#FBBF24"),
        ["OverlayScrim"] = Color.FromArgb("#000000B3"),
    };

    public AppThemePreference Preference { get; private set; }

    public event EventHandler? ThemeChanged;

    public ThemeService()
    {
        var stored = Preferences.Default.Get(PreferenceKey, nameof(AppThemePreference.Dark));
        Preference = Enum.TryParse<AppThemePreference>(stored, out var p)
            ? p
            : AppThemePreference.Dark;
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
