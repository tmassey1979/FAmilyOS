namespace FamilyOS.Maui.Controls;

/// <summary>
/// Top app bar: title, optional subtitle, leading (back) action, trailing content slot.
/// Theme-aware surface; no raw colors.
/// </summary>
public partial class FoAppBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FoAppBar), string.Empty,
            propertyChanged: (b, _, n) => ((FoAppBar)b).TitleLabel.Text = (string)n);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(FoAppBar), string.Empty,
            propertyChanged: (b, _, n) => ((FoAppBar)b).ApplySubtitle((string)n));

    public static readonly BindableProperty ShowLeadingProperty =
        BindableProperty.Create(nameof(ShowLeading), typeof(bool), typeof(FoAppBar), false,
            propertyChanged: (b, _, n) => ((FoAppBar)b).LeadingButton.IsVisible = (bool)n);

    public static readonly BindableProperty LeadingTextProperty =
        BindableProperty.Create(nameof(LeadingText), typeof(string), typeof(FoAppBar), "‹",
            propertyChanged: (b, _, n) => ((FoAppBar)b).LeadingButton.Text = (string)n);

    public event EventHandler? LeadingClicked;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool ShowLeading
    {
        get => (bool)GetValue(ShowLeadingProperty);
        set => SetValue(ShowLeadingProperty, value);
    }

    public string LeadingText
    {
        get => (string)GetValue(LeadingTextProperty);
        set => SetValue(LeadingTextProperty, value);
    }

    /// <summary>Optional trailing views (e.g. FoButton, FoAvatar).</summary>
    public IList<IView> Trailing => TrailingHost.Children;

    public FoAppBar()
    {
        InitializeComponent();
        TitleLabel.Text = Title;
        ApplySubtitle(Subtitle);
        LeadingButton.IsVisible = ShowLeading;
        SemanticProperties.SetHeadingLevel(TitleLabel, SemanticHeadingLevel.Level1);
    }

    private void ApplySubtitle(string subtitle)
    {
        SubtitleLabel.Text = subtitle;
        SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(subtitle);
    }

    private void OnLeadingClicked(object? sender, EventArgs e) =>
        LeadingClicked?.Invoke(this, e);
}
