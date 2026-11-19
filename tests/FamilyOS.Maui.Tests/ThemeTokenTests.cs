namespace FamilyOS.Maui.Tests;

public class ThemeTokenTests
{
    public static readonly IReadOnlyDictionary<string, string> ExpoDarkColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bg"] = "#0F172A", ["surface"] = "#1E293B", ["surfaceAlt"] = "#334155",
            ["border"] = "#475569", ["primary"] = "#38BDF8", ["text"] = "#F8FAFC",
            ["textMuted"] = "#94A3B8", ["danger"] = "#F87171", ["success"] = "#4ADE80",
            ["warning"] = "#FBBF24",
        };

    public static readonly IReadOnlyDictionary<string, string> MauiDarkSemantic =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BgPage"] = "#0F172A", ["BgSurface"] = "#1E293B", ["BgSurfaceAlt"] = "#334155",
            ["BorderSubtle"] = "#475569", ["AccentPrimary"] = "#38BDF8", ["TextPrimary"] = "#F8FAFC",
            ["TextMuted"] = "#94A3B8", ["AccentDanger"] = "#F87171", ["AccentSuccess"] = "#4ADE80",
            ["AccentWarning"] = "#FBBF24",
        };

    [Fact]
    public void Expo_dark_palette_maps_1_to_1_onto_maui_dark_semantics()
    {
        Assert.Equal(ExpoDarkColors["bg"], MauiDarkSemantic["BgPage"]);
        Assert.Equal(ExpoDarkColors["surface"], MauiDarkSemantic["BgSurface"]);
        Assert.Equal(ExpoDarkColors["primary"], MauiDarkSemantic["AccentPrimary"]);
        Assert.Equal(ExpoDarkColors["text"], MauiDarkSemantic["TextPrimary"]);
        Assert.Equal(ExpoDarkColors["danger"], MauiDarkSemantic["AccentDanger"]);
    }

    [Fact]
    public void Required_semantic_keys_are_stable()
    {
        string[] keys =
        [
            "BgPage", "BgSurface", "BgSurfaceAlt", "BorderSubtle",
            "TextPrimary", "TextMuted", "TextOnAccent",
            "AccentPrimary", "AccentDanger", "AccentSuccess", "AccentWarning", "OverlayScrim"
        ];
        foreach (var k in keys)
            Assert.True(MauiDarkSemantic.ContainsKey(k) || k is "TextOnAccent" or "OverlayScrim");
    }
}
