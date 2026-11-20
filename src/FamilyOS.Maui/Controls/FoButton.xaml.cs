namespace FamilyOS.Maui.Controls;

public enum FoButtonVariant { Primary, Secondary, Danger, Ghost }

public partial class FoButton : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FoButton), string.Empty,
            propertyChanged: (b, _, n) => ((FoButton)b).ApplyTitle((string)n));
    public static readonly BindableProperty VariantProperty =
        BindableProperty.Create(nameof(Variant), typeof(FoButtonVariant), typeof(FoButton), FoButtonVariant.Primary,
            propertyChanged: (b, _, _) => ((FoButton)b).ApplyVariant());
    public static readonly BindableProperty IsLoadingProperty =
        BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(FoButton), false,
            propertyChanged: (b, _, _) => ((FoButton)b).ApplyLoading());

    public event EventHandler? Clicked;
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public FoButtonVariant Variant { get => (FoButtonVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    public FoButton()
    {
        InitializeComponent();
        ApplyVariant();
        ApplyTitle(Title);
    }

    private void ApplyTitle(string title)
    {
        if (!IsLoading) InnerButton.Text = title;
        SemanticProperties.SetDescription(InnerButton, title);
    }

    private void ApplyVariant()
    {
        InnerButton.Style = Variant switch
        {
            FoButtonVariant.Secondary => (Style)Application.Current!.Resources["FoSecondaryButtonStyle"],
            FoButtonVariant.Danger => (Style)Application.Current!.Resources["FoDangerButtonStyle"],
            FoButtonVariant.Ghost => (Style)Application.Current!.Resources["FoGhostButtonStyle"],
            _ => (Style)Application.Current!.Resources["FoPrimaryButtonStyle"]
        };
    }

    private void ApplyLoading()
    {
        InnerButton.IsEnabled = !IsLoading && IsEnabled;
        InnerButton.Text = IsLoading ? "…" : Title;
        SemanticProperties.SetHint(InnerButton, IsLoading ? "Loading" : null);
    }

    private void OnInnerClicked(object? sender, EventArgs e)
    {
        if (!IsLoading) Clicked?.Invoke(this, e);
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled))
            InnerButton.IsEnabled = IsEnabled && !IsLoading;
    }
}
