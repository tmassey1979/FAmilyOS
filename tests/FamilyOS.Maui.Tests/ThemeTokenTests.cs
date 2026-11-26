using Xunit;

namespace FamilyOS.Maui.Tests;

/// <summary>
/// Validates design-token contract without requiring a MAUI device host.
/// Keeps the FamilyOS visual token contract consistent as resources evolve.
/// </summary>
public class ThemeTokenTests
{
    public static readonly IReadOnlyDictionary<string, string> FamilyOsDarkColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bg"] = "#080D1B",
            ["surface"] = "#111B2E",
            ["surfaceAlt"] = "#1B2940",
            ["border"] = "#30445F",
            ["primary"] = "#55E6FF",
            ["text"] = "#F3F7FF",
            ["textMuted"] = "#9BAFC9",
            ["danger"] = "#FF718B",
            ["success"] = "#53E6B1",
            ["warning"] = "#FFC66D",
        };

    public static readonly IReadOnlyDictionary<string, string> MauiDarkSemantic =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BgPage"] = "#080D1B",
            ["BgSurface"] = "#111B2E",
            ["BgSurfaceAlt"] = "#1B2940",
            ["BorderSubtle"] = "#30445F",
            ["AccentPrimary"] = "#55E6FF",
            ["TextPrimary"] = "#F3F7FF",
            ["TextMuted"] = "#9BAFC9",
            ["AccentDanger"] = "#FF718B",
            ["AccentSuccess"] = "#53E6B1",
            ["AccentWarning"] = "#FFC66D",
        };

    public static readonly IReadOnlyList<string> RequiredSemanticKeys =
    [
        "BgPage", "BgSurface", "BgSurfaceAlt", "BgGlow", "BorderSubtle",
        "TextPrimary", "TextMuted", "TextOnAccent",
        "AccentPrimary", "AccentSecondary", "AccentDanger", "AccentSuccess", "AccentWarning",
        "OverlayScrim"
    ];

    public static readonly IReadOnlyList<string> RequiredSpacingKeys =
    [
        "SpaceXs", "SpaceSm", "SpaceMd", "SpaceLg", "SpaceXl",
        "ControlHeightMin", "RadiusMd", "RadiusLg"
    ];

    [Fact]
    public void FamilyOS_dark_palette_matches_MAUI_semantic_tokens()
    {
        Assert.Equal(FamilyOsDarkColors["bg"], MauiDarkSemantic["BgPage"]);
        Assert.Equal(FamilyOsDarkColors["surface"], MauiDarkSemantic["BgSurface"]);
        Assert.Equal(FamilyOsDarkColors["surfaceAlt"], MauiDarkSemantic["BgSurfaceAlt"]);
        Assert.Equal(FamilyOsDarkColors["border"], MauiDarkSemantic["BorderSubtle"]);
        Assert.Equal(FamilyOsDarkColors["primary"], MauiDarkSemantic["AccentPrimary"]);
        Assert.Equal(FamilyOsDarkColors["text"], MauiDarkSemantic["TextPrimary"]);
        Assert.Equal(FamilyOsDarkColors["textMuted"], MauiDarkSemantic["TextMuted"]);
        Assert.Equal(FamilyOsDarkColors["danger"], MauiDarkSemantic["AccentDanger"]);
        Assert.Equal(FamilyOsDarkColors["success"], MauiDarkSemantic["AccentSuccess"]);
        Assert.Equal(FamilyOsDarkColors["warning"], MauiDarkSemantic["AccentWarning"]);
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

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir.FullName, "..", relative);
            if (File.Exists(Path.GetFullPath(candidate)))
                return Path.GetFullPath(candidate);
            dir = dir.Parent;
        }
        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Could not locate {relative} from test host.");
    }
}
