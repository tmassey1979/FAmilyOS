using Microsoft.Maui.Controls.Shapes;

namespace FamilyOS.Maui.Controls;

/// <summary>
/// Circular avatar with initials fallback. Theme-aware surface/accent.
/// </summary>
public partial class FoAvatar : ContentView
{
    public static readonly BindableProperty InitialsProperty =
        BindableProperty.Create(nameof(Initials), typeof(string), typeof(FoAvatar), "?",
            propertyChanged: (b, _, n) => ((FoAvatar)b).ApplyInitials((string)n));

    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(FoAvatar), 40d,
            propertyChanged: (b, _, n) => ((FoAvatar)b).ApplySize((double)n));

    public string Initials
    {
        get => (string)GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public FoAvatar()
    {
        InitializeComponent();
        ApplyThemeColors();
        ApplyInitials(Initials);
        ApplySize(Size);
    }

    private void ApplyInitials(string initials)
    {
        var text = string.IsNullOrWhiteSpace(initials) ? "?" : initials.Trim().ToUpperInvariant();
        if (text.Length > 2) text = text[..2];
        InitialsLabel.Text = text;
        SemanticProperties.SetDescription(Root, $"Avatar {text}");
    }

    private void ApplySize(double size)
    {
        Root.WidthRequest = size;
        Root.HeightRequest = size;
        if (Root.StrokeShape is RoundRectangle rr)
            rr.CornerRadius = size / 2;
        InitialsLabel.FontSize = Math.Max(10, size * 0.35);
    }

    private void ApplyThemeColors()
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;
        Root.BackgroundColor = resources.TryGetValue("AccentPrimary", out var a) && a is Color ac ? ac : Colors.SkyBlue;
        InitialsLabel.TextColor = resources.TryGetValue("TextOnAccent", out var t) && t is Color tc ? tc : Colors.Black;
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        ApplyThemeColors();
    }
}
