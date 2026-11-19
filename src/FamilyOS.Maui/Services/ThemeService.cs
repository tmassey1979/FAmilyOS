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
        ["BgPage"] = Color.FromArgb("#F8F5F0"),
        ["BgSurface"] = Color.FromArgb("#FFFFFF"),
        ["BgSurfaceAlt"] = Color.FromArgb("#F1EDE6"),
        ["BorderSubtle"] = Color.FromArgb("#E2E8F0"),
        ["TextPrimary"] = Color.FromArgb("#0F172A"),
        ["TextMuted"] = Color.FromArgb("#64748B"),
        ["TextOnAccent"] = Color.FromArgb("#0F172A"),
        ["AccentPrimary"] = Color.FromArgb("#38BDF8"),
        ["AccentDanger"] = Color.FromArgb("#F87171"),
        ["AccentSuccess"] = Color.FromArgb("#4ADE80"),
        ["AccentWarning"] = Color.FromArgb("#FBBF24"),
        ["OverlayScrim"] = Color.FromArgb("#00000066"),
    };

    private static readonly Dictionary<string, Color> Dark = new(StringComparer.Ordinal)
    {
        ["BgPage"] = Color.FromArgb("#0F172A"),
        ["BgSurface"] = Color.FromArgb("#1E293B"),
        ["BgSurfaceAlt"] = Color.FromArgb("#334155"),
        ["BorderSubtle"] = Color.FromArgb("#475569"),
        ["TextPrimary"] = Color.FromArgb("#F8FAFC"),
        ["TextMuted"] = Color.FromArgb("#94A3B8"),
        ["TextOnAccent"] = Color.FromArgb("#0F172A"),
        ["AccentPrimary"] = Color.FromArgb("#38BDF8"),
        ["AccentDanger"] = Color.FromArgb("#F87171"),
        ["AccentSuccess"] = Color.FromArgb("#4ADE80"),
        ["AccentWarning"] = Color.FromArgb("#FBBF24"),
        ["OverlayScrim"] = Color.FromArgb("#00000099"),
    };

    public AppThemePreference Preference { get; private set; }

    public event EventHandler? ThemeChanged;

    public ThemeService()
    {
        var stored = Preferences.Default.Get(PreferenceKey, nameof(AppThemePreference.System));
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
