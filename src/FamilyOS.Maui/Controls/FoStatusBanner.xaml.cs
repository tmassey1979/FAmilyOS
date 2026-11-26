namespace FamilyOS.Maui.Controls;

public enum FoStatusKind { Info, Success, Warning, Danger, Offline }

public partial class FoStatusBanner : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(FoStatusBanner), string.Empty,
            propertyChanged: (b, _, n) => ((FoStatusBanner)b).MessageLabel.Text = (string)n);
    public static readonly BindableProperty KindProperty =
        BindableProperty.Create(nameof(Kind), typeof(FoStatusKind), typeof(FoStatusBanner), FoStatusKind.Info,
            propertyChanged: (b, _, _) => ((FoStatusBanner)b).ApplyKind());
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public FoStatusKind Kind { get => (FoStatusKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public FoStatusBanner() { InitializeComponent(); ApplyKind(); }
    private void ApplyKind()
    {
        var key = Kind switch
        {
            FoStatusKind.Success => "AccentSuccess",
            FoStatusKind.Warning => "AccentWarning",
            FoStatusKind.Danger => "AccentDanger",
            FoStatusKind.Offline => "BgSurfaceAlt",
            _ => "AccentPrimary"
        };
        var resources = Application.Current!.Resources;
        var accent = (Color)resources[key];
        if (Kind == FoStatusKind.Offline)
        {
            BannerBorder.BackgroundColor = (Color)resources["BgSurfaceAlt"];
            BannerBorder.Stroke = (Color)resources["BorderSubtle"];
            MessageLabel.TextColor = (Color)resources["TextPrimary"];
        }
        else
        {
            // Soft tint keeps semantic text readable in both themes.
            BannerBorder.BackgroundColor = accent.WithAlpha(0.14f);
            BannerBorder.Stroke = accent.WithAlpha(0.45f);
            BannerBorder.StrokeThickness = 1;
            MessageLabel.TextColor = accent;
        }
        SemanticProperties.SetDescription(this, $"{Kind}: {Message}");
    }
}
