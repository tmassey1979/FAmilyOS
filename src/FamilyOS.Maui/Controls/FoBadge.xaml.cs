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
        var key = Tone switch
        {
            FoBadgeTone.Success => "AccentSuccess",
            FoBadgeTone.Warning => "AccentWarning",
            FoBadgeTone.Danger => "AccentDanger",
            FoBadgeTone.Info => "AccentPrimary",
            _ => "BgSurfaceAlt"
        };
        var resources = Application.Current!.Resources;
        if (Tone == FoBadgeTone.Neutral)
        {
            BadgeBorder.BackgroundColor = (Color)resources["BgSurfaceAlt"];
            BadgeLabel.TextColor = (Color)resources["TextMuted"];
        }
        else
        {
            var accent = (Color)resources[key];
            BadgeBorder.BackgroundColor = accent.WithAlpha(0.14f);
            BadgeBorder.Stroke = accent.WithAlpha(0.45f);
            BadgeBorder.StrokeThickness = 1;
            BadgeLabel.TextColor = accent;
        }
        SemanticProperties.SetDescription(this, $"{Tone}: {Text}");
    }
}
