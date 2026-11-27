using Xunit;

namespace FamilyOS.Maui.Tests;

/// <summary>
/// Validates design-token contract without requiring a MAUI device host.
/// Sci-fi command-center palette (dark-first electric cyan).
/// </summary>
public class ThemeTokenTests
{
    public static readonly IReadOnlyDictionary<string, string> ExpoDarkColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bg"] = "#0A0E14",
            ["surface"] = "#121A2B",
            ["surfaceAlt"] = "#1A2438",
            ["border"] = "#2A3A55",
            ["primary"] = "#22D3EE",
            ["text"] = "#F1F5F9",
            ["textMuted"] = "#94A3B8",
            ["danger"] = "#FB7185",
            ["success"] = "#34D399",
            ["warning"] = "#FBBF24",
        };

    public static readonly IReadOnlyDictionary<string, string> MauiDarkSemantic =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BgPage"] = "#0A0E14",
            ["BgSurface"] = "#121A2B",
            ["BgSurfaceAlt"] = "#1A2438",
            ["BorderSubtle"] = "#2A3A55",
            ["AccentPrimary"] = "#22D3EE",
            ["TextPrimary"] = "#F1F5F9",
            ["TextMuted"] = "#94A3B8",
            ["AccentDanger"] = "#FB7185",
            ["AccentSuccess"] = "#34D399",
            ["AccentWarning"] = "#FBBF24",
        };

    public static readonly IReadOnlyList<string> RequiredSemanticKeys =
    [
        "BgPage", "BgSurface", "BgSurfaceAlt", "BorderSubtle",
        "TextPrimary", "TextMuted", "TextOnAccent",
        "AccentPrimary", "AccentDanger", "AccentSuccess", "AccentWarning",
        "OverlayScrim"
    ];

    public static readonly IReadOnlyList<string> RequiredSpacingKeys =
    [
        "SpaceXs", "SpaceSm", "SpaceMd", "SpaceLg", "SpaceXl",
        "ControlHeightMin", "RadiusMd", "RadiusLg"
    ];

    [Fact]
    public void Expo_dark_palette_maps_1_to_1_onto_maui_dark_semantics()
    {
        Assert.Equal(ExpoDarkColors["bg"], MauiDarkSemantic["BgPage"]);
        Assert.Equal(ExpoDarkColors["surface"], MauiDarkSemantic["BgSurface"]);
        Assert.Equal(ExpoDarkColors["surfaceAlt"], MauiDarkSemantic["BgSurfaceAlt"]);
        Assert.Equal(ExpoDarkColors["border"], MauiDarkSemantic["BorderSubtle"]);
        Assert.Equal(ExpoDarkColors["primary"], MauiDarkSemantic["AccentPrimary"]);
        Assert.Equal(ExpoDarkColors["text"], MauiDarkSemantic["TextPrimary"]);
        Assert.Equal(ExpoDarkColors["textMuted"], MauiDarkSemantic["TextMuted"]);
        Assert.Equal(ExpoDarkColors["danger"], MauiDarkSemantic["AccentDanger"]);
        Assert.Equal(ExpoDarkColors["success"], MauiDarkSemantic["AccentSuccess"]);
        Assert.Equal(ExpoDarkColors["warning"], MauiDarkSemantic["AccentWarning"]);
    }

    [Fact]
    public void Dark_theme_xaml_declares_all_semantic_keys()
    {
        var path = FindRepoFile("src/FamilyOS.Maui/Resources/Themes/DarkTheme.xaml");
        var xaml = File.ReadAllText(path);
        foreach (var key in RequiredSemanticKeys)
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Light_theme_xaml_declares_all_semantic_keys()
    {
        var path = FindRepoFile("src/FamilyOS.Maui/Resources/Themes/LightTheme.xaml");
        var xaml = File.ReadAllText(path);
        foreach (var key in RequiredSemanticKeys)
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Spacing_dictionary_declares_core_scale()
    {
        var path = FindRepoFile("src/FamilyOS.Maui/Resources/Styles/Spacing.xaml");
        var xaml = File.ReadAllText(path);
        foreach (var key in RequiredSpacingKeys)
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_controls_exist_on_disk()
    {
        string[] controls =
        [
            "FoButton", "FoCard", "FoBadge", "FoSectionTitle", "FoEmptyState",
            "FoStatusBanner", "FoListRow", "FoTextField", "FoLoadingView", "FoErrorView",
            "FoChip", "FoAvatar",
            "FoAppBar", "FoDialog", "FoSheet"
        ];
        foreach (var name in controls)
        {
            var path = FindRepoFile($"src/FamilyOS.Maui/Controls/{name}.xaml");
            Assert.True(File.Exists(path), $"Missing control {name}");
        }
    }

    [Fact]
    public void Design_system_docs_exist()
    {
        string[] docs =
        [
            "docs/design-system/README.md",
            "docs/design-system/PRINCIPLES.md",
            "docs/design-system/TOKENS.md",
            "docs/design-system/COMPONENTS.md",
            "docs/design-system/EXPO_MAPPING.md",
            "docs/design-system/ADDING_A_SCREEN.md"
        ];
        foreach (var rel in docs)
            Assert.True(File.Exists(FindRepoFile(rel)), $"Missing {rel}");
    }

    [Fact]
    public void Button_styles_are_declared()
    {
        var path = FindRepoFile("src/FamilyOS.Maui/Resources/Styles/Styles.xaml");
        var xaml = File.ReadAllText(path);
        foreach (var key in new[] { "FoPrimaryButtonStyle", "FoSecondaryButtonStyle", "FoDangerButtonStyle", "FoGhostButtonStyle", "FoAccentButtonStyle", "FoCardFrameStyle", "FoCardAccentFrameStyle" })
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
            if (File.Exists(Path.Combine(dir.FullName, "FamilyOS.sln")) ||
                File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            {
                candidate = Path.Combine(dir.FullName, relative);
                if (File.Exists(candidate))
                    return candidate;
            }
            dir = dir.Parent;
        }

        var fromCwd = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relative));
        if (File.Exists(fromCwd))
            return fromCwd;

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
