namespace FamilyOS.Maui.Controls;

/// <summary>
/// Bottom sheet: drag-handle visual, title, optional subtitle, body slot, optional actions.
/// Host full-page; toggle visibility on the parent overlay.
/// </summary>
public partial class FoSheet : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FoSheet), string.Empty,
            propertyChanged: (b, _, n) => ((FoSheet)b).TitleLabel.Text = (string)n);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(FoSheet), string.Empty,
            propertyChanged: (b, _, n) => ((FoSheet)b).ApplySubtitle((string)n));

    public static readonly BindableProperty PrimaryTextProperty =
        BindableProperty.Create(nameof(PrimaryText), typeof(string), typeof(FoSheet), string.Empty,
            propertyChanged: (b, _, n) => ((FoSheet)b).ApplyPrimary((string)n));

    public static readonly BindableProperty SecondaryTextProperty =
        BindableProperty.Create(nameof(SecondaryText), typeof(string), typeof(FoSheet), string.Empty,
            propertyChanged: (b, _, n) => ((FoSheet)b).ApplySecondary((string)n));

    public static readonly BindableProperty DismissOnScrimProperty =
        BindableProperty.Create(nameof(DismissOnScrim), typeof(bool), typeof(FoSheet), true);

    public event EventHandler? PrimaryClicked;
    public event EventHandler? SecondaryClicked;
    public event EventHandler? CloseClicked;
    public event EventHandler? Dismissed;

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

    public string PrimaryText
    {
        get => (string)GetValue(PrimaryTextProperty);
        set => SetValue(PrimaryTextProperty, value);
    }

    public string SecondaryText
    {
        get => (string)GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    public bool DismissOnScrim
    {
        get => (bool)GetValue(DismissOnScrimProperty);
        set => SetValue(DismissOnScrimProperty, value);
    }

    public View? Body
    {
        get => BodyHost.Content;
        set => BodyHost.Content = value;
    }

    public FoSheet()
    {
        InitializeComponent();
        TitleLabel.Text = Title;
        ApplySubtitle(Subtitle);
        ApplyPrimary(PrimaryText);
        ApplySecondary(SecondaryText);
        SemanticProperties.SetDescription(Panel, "Bottom sheet");
    }

    private void ApplySubtitle(string subtitle)
    {
        SubtitleLabel.Text = subtitle;
        SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(subtitle);
    }

    private void ApplyPrimary(string text)
    {
        var show = !string.IsNullOrWhiteSpace(text);
        PrimaryButton.IsVisible = show;
        if (show) PrimaryButton.Title = text;
        RefreshActionsVisibility();
    }

    private void ApplySecondary(string text)
    {
        var show = !string.IsNullOrWhiteSpace(text);
        SecondaryButton.IsVisible = show;
        if (show) SecondaryButton.Title = text;
        RefreshActionsVisibility();
    }

    private void RefreshActionsVisibility() =>
        ActionsHost.IsVisible = PrimaryButton.IsVisible || SecondaryButton.IsVisible;

    private void OnPrimaryClicked(object? sender, EventArgs e) =>
        PrimaryClicked?.Invoke(this, e);

    private void OnSecondaryClicked(object? sender, EventArgs e) =>
        SecondaryClicked?.Invoke(this, e);

    private void OnCloseClicked(object? sender, EventArgs e)
    {
        CloseClicked?.Invoke(this, e);
        Dismissed?.Invoke(this, e);
    }

    private void OnScrimTapped(object? sender, TappedEventArgs e)
    {
        if (!DismissOnScrim) return;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}
