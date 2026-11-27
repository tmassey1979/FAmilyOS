namespace FamilyOS.Maui.Controls;

public enum FoCardTone { Default, Accent, Success, Warning, Danger }

public partial class FoCard : ContentView
{
    public static readonly BindableProperty ToneProperty =
        BindableProperty.Create(nameof(Tone), typeof(FoCardTone), typeof(FoCard), FoCardTone.Default,
            propertyChanged: (b, _, _) => ((FoCard)b).ApplyTone());

    public FoCardTone Tone
    {
        get => (FoCardTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public FoCard()
    {
        InitializeComponent();
        ApplyTone();
    }

    private void ApplyTone()
    {
        if (Application.Current?.Resources is null) return;

        if (Tone == FoCardTone.Accent &&
            Application.Current.Resources.TryGetValue("FoCardAccentFrameStyle", out var accentStyle) &&
            accentStyle is Style s)
        {
            Frame.Style = s;
            return;
        }

        if (Application.Current.Resources.TryGetValue("FoCardFrameStyle", out var baseStyle) &&
            baseStyle is Style bs)
            Frame.Style = bs;

        Color stroke = Tone switch
        {
            FoCardTone.Success => GetColor("AccentSuccess"),
            FoCardTone.Warning => GetColor("AccentWarning"),
            FoCardTone.Danger => GetColor("AccentDanger"),
            FoCardTone.Accent => GetColor("AccentPrimary"),
            _ => GetColor("BorderSubtle")
        };

        Frame.Stroke = stroke;
        Frame.StrokeThickness = Tone == FoCardTone.Default ? 1 : 1.5;
    }

    private static Color GetColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c
            ? c
            : Colors.Gray;
}
