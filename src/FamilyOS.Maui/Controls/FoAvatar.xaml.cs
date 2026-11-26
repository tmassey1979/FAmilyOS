using Microsoft.Maui.Controls.Shapes;

namespace FamilyOS.Maui.Controls;

/// <summary>
/// Circular avatar with photo when available, otherwise initials fallback. Theme-aware surface/accent.
/// </summary>
public partial class FoAvatar : ContentView
{
    public static readonly BindableProperty InitialsProperty =
        BindableProperty.Create(nameof(Initials), typeof(string), typeof(FoAvatar), "?",
            propertyChanged: (b, _, n) => ((FoAvatar)b).ApplyInitials((string)n));

    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(FoAvatar), 40d,
            propertyChanged: (b, _, n) => ((FoAvatar)b).ApplySize((double)n));

    public static readonly BindableProperty ImageSourceProperty =
        BindableProperty.Create(nameof(ImageSource), typeof(ImageSource), typeof(FoAvatar), null,
            propertyChanged: (b, _, n) => ((FoAvatar)b).ApplyImage((ImageSource?)n));

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

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public FoAvatar()
    {
        InitializeComponent();
        ApplySize(Size);
        ApplyInitials(Initials);
    }

    public void SetAvatarUrl(string? url, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            ImageSource = null;
            return;
        }
        var absolute = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : (baseUrl?.TrimEnd('/') + "/" + url.TrimStart('/'));
        ImageSource = ImageSource.FromUri(new Uri(absolute!));
    }

    private void ApplyInitials(string text)
    {
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

    private void ApplyImage(ImageSource? source)
    {
        if (source is null)
        {
            Photo.Source = null;
            Photo.IsVisible = false;
            InitialsLabel.IsVisible = true;
            return;
        }
        Photo.Source = source;
        Photo.IsVisible = true;
        InitialsLabel.IsVisible = false;
    }
}
