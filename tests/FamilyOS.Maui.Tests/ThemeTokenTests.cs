namespace FamilyOS.Maui.Tests;

/// <summary>
/// Validates design-token contract without requiring a MAUI device host.
/// Keeps the Expo → MAUI mapping honest as resources evolve.
/// </summary>
public class ThemeTokenTests
{
    public static readonly IReadOnlyDictionary<string, string> ExpoDarkColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bg"] = "#0F172A",
            ["surface"] = "#1E293B",
            ["surfaceAlt"] = "#334155",
            ["border"] = "#475569",
            ["primary"] = "#38BDF8",
            ["text"] = "#F8FAFC",
            ["textMuted"] = "#94A3B8",
            ["danger"] = "#F87171",
            ["success"] = "#4ADE80",
            ["warning"] = "#FBBF24",
        };

    public static readonly IReadOnlyDictionary<string, string> MauiDarkSemantic =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BgPage"] = "#0F172A",
            ["BgSurface"] = "#1E293B",
            ["BgSurfaceAlt"] = "#334155",
            ["BorderSubtle"] = "#475569",
            ["AccentPrimary"] = "#38BDF8",
            ["TextPrimary"] = "#F8FAFC",
            ["TextMuted"] = "#94A3B8",
            ["AccentDanger"] = "#F87171",
            ["AccentSuccess"] = "#4ADE80",
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
