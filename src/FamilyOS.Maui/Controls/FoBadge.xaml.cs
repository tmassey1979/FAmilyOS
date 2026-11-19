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
        BadgeBorder.BackgroundColor = (Color)Application.Current!.Resources[key];
        SemanticProperties.SetDescription(this, $"{Tone}: {Text}");
    }
}
