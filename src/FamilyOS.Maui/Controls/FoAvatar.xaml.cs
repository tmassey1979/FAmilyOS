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
            propertyChanged: (b, _, n) => ((FoAvatar)b).ApplyImage(n as ImageSource));

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

    /// <summary>Resolve a relative avatar path (e.g. /avatars/x.jpg) against the API base URL.</summary>
    public void SetAvatarUrl(string? avatarUrl, string? apiBaseUrl, string displayName)
    {
        Initials = InitialsFromName(displayName);
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            ImageSource = null;
            return;
        }

        var absolute = avatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? avatarUrl
            : $"{(apiBaseUrl ?? "").TrimEnd('/')}/{avatarUrl.TrimStart('/')}";
        ImageSource = ImageSource.FromUri(new Uri(absolute));
    }

    public static string InitialsFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)];
        return $"{parts[0][0]}{parts[^1][0]}";
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
