namespace FamilyOS.Maui.Controls;

public enum FoChipTone { Neutral, Info, Success, Warning, Danger }

/// <summary>
/// Compact selectable/filter chip. Uses semantic theme colors only.
/// </summary>
public partial class FoChip : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FoChip), string.Empty,
            propertyChanged: (b, _, n) => ((FoChip)b).ApplyText((string)n));

    public static readonly BindableProperty ToneProperty =
        BindableProperty.Create(nameof(Tone), typeof(FoChipTone), typeof(FoChip), FoChipTone.Neutral,
            propertyChanged: (b, _, _) => ((FoChip)b).ApplyTone());

    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(FoChip), false,
            propertyChanged: (b, _, _) => ((FoChip)b).ApplyTone());

    public event EventHandler? Tapped;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FoChipTone Tone
    {
        get => (FoChipTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public FoChip()
    {
        InitializeComponent();
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            IsSelected = !IsSelected;
            Tapped?.Invoke(this, EventArgs.Empty);
        };
        Root.GestureRecognizers.Add(tap);
        ApplyTone();
        ApplyText(Text);
    }

    private void ApplyText(string text)
    {
        Label.Text = text;
        SemanticProperties.SetDescription(Root, text);
    }

    private void ApplyTone()
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        Color accent = Tone switch
        {
            FoChipTone.Info => GetColor(resources, "AccentPrimary"),
            FoChipTone.Success => GetColor(resources, "AccentSuccess"),
            FoChipTone.Warning => GetColor(resources, "AccentWarning"),
            FoChipTone.Danger => GetColor(resources, "AccentDanger"),
            _ => GetColor(resources, "BorderSubtle")
        };

        var surface = GetColor(resources, "BgSurface");
        var muted = GetColor(resources, "TextMuted");

        if (IsSelected)
        {
            Root.BackgroundColor = accent;
            Root.Stroke = accent;
            Label.TextColor = GetColor(resources, "TextOnAccent");
        }
        else
        {
            Root.BackgroundColor = surface;
            Root.Stroke = accent;
            Label.TextColor = Tone == FoChipTone.Neutral ? muted : accent;
        }

        SemanticProperties.SetHint(Root, IsSelected ? "Selected" : "Not selected");
    }

    private static Color GetColor(ResourceDictionary resources, string key) =>
        resources.TryGetValue(key, out var v) && v is Color c ? c : Colors.Gray;
}
