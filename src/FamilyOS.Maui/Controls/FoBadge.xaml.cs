namespace FamilyOS.Maui.Controls;

public enum FoBadgeTone { Neutral, Info, Success, Warning, Danger }

public partial class FoBadge : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FoBadge), string.Empty,
            propertyChanged: (b, _, n) => ((FoBadge)b).BadgeLabel.Text = (string)n);
    public static readonly BindableProperty ToneProperty =
        BindableProperty.Create(nameof(Tone), typeof(FoBadgeTone), typeof(FoBadge), FoBadgeTone.Neutral,
            propertyChanged: (b, _, _) => ((FoBadge)b).ApplyTone());

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FoBadgeTone Tone { get => (FoBadgeTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public FoBadge()
    {
        InitializeComponent();
        ApplyTone();
    }

    private void ApplyTone()
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var key = Tone switch
        {
            FoBadgeTone.Success => "AccentSuccess",
            FoBadgeTone.Warning => "AccentWarning",
            FoBadgeTone.Danger => "AccentDanger",
            FoBadgeTone.Info => "AccentPrimary",
            _ => "BgSurfaceAlt"
        };

        BadgeBorder.BackgroundColor = resources.TryGetValue(key, out var bg) && bg is Color c
            ? c
            : Colors.Gray;

        if (Tone == FoBadgeTone.Neutral)
        {
            BadgeLabel.TextColor = resources.TryGetValue("TextPrimary", out var tp) && tp is Color tpc ? tpc : Colors.White;
            BadgeBorder.StrokeThickness = 1;
            if (resources.TryGetValue("BorderSubtle", out var br) && br is Color bc)
                BadgeBorder.Stroke = bc;
        }
        else
        {
            BadgeLabel.TextColor = resources.TryGetValue("TextOnAccent", out var ink) && ink is Color ik
                ? ik
                : Color.FromArgb("#041018");
            BadgeBorder.StrokeThickness = 0;
        }

        SemanticProperties.SetDescription(this, $"{Tone}: {Text}");
    }
}
