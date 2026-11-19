namespace FamilyOS.Maui.Controls;

/// <summary>
/// Modal dialog: scrim, title, message, optional body content, primary/secondary actions.
/// Place in a full-page overlay; toggle visibility on the host.
/// </summary>
public partial class FoDialog : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FoDialog), string.Empty,
            propertyChanged: (b, _, n) => ((FoDialog)b).TitleLabel.Text = (string)n);

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(FoDialog), string.Empty,
            propertyChanged: (b, _, n) => ((FoDialog)b).ApplyMessage((string)n));

    public static readonly BindableProperty PrimaryTextProperty =
        BindableProperty.Create(nameof(PrimaryText), typeof(string), typeof(FoDialog), "OK",
            propertyChanged: (b, _, n) => ((FoDialog)b).PrimaryButton.Title = (string)n);

    public static readonly BindableProperty SecondaryTextProperty =
        BindableProperty.Create(nameof(SecondaryText), typeof(string), typeof(FoDialog), "Cancel",
            propertyChanged: (b, _, n) => ((FoDialog)b).ApplySecondary((string)n));

    public static readonly BindableProperty ShowSecondaryProperty =
        BindableProperty.Create(nameof(ShowSecondary), typeof(bool), typeof(FoDialog), true,
            propertyChanged: (b, _, n) => ((FoDialog)b).SecondaryButton.IsVisible = (bool)n);

    public static readonly BindableProperty DismissOnScrimProperty =
        BindableProperty.Create(nameof(DismissOnScrim), typeof(bool), typeof(FoDialog), true);

    public static readonly BindableProperty IsDangerPrimaryProperty =
        BindableProperty.Create(nameof(IsDangerPrimary), typeof(bool), typeof(FoDialog), false,
            propertyChanged: (b, _, n) =>
                ((FoDialog)b).PrimaryButton.Variant = (bool)n ? FoButtonVariant.Danger : FoButtonVariant.Primary);

    public event EventHandler? PrimaryClicked;
    public event EventHandler? SecondaryClicked;
    public event EventHandler? Dismissed;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
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

    public bool ShowSecondary
    {
        get => (bool)GetValue(ShowSecondaryProperty);
        set => SetValue(ShowSecondaryProperty, value);
    }

    public bool DismissOnScrim
    {
        get => (bool)GetValue(DismissOnScrimProperty);
        set => SetValue(DismissOnScrimProperty, value);
    }

    public bool IsDangerPrimary
    {
        get => (bool)GetValue(IsDangerPrimaryProperty);
        set => SetValue(IsDangerPrimaryProperty, value);
    }

    /// <summary>Optional custom body below the message.</summary>
    public View? Body
    {
        get => BodyHost.Content;
        set
        {
            BodyHost.Content = value;
            BodyHost.IsVisible = value is not null;
        }
    }

    public FoDialog()
    {
        InitializeComponent();
        TitleLabel.Text = Title;
        ApplyMessage(Message);
        PrimaryButton.Title = PrimaryText;
        ApplySecondary(SecondaryText);
        SecondaryButton.IsVisible = ShowSecondary;
        SemanticProperties.SetDescription(Card, "Dialog");
    }

    private void ApplyMessage(string message)
    {
        MessageLabel.Text = message;
        MessageLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private void ApplySecondary(string text)
    {
        SecondaryButton.Title = string.IsNullOrWhiteSpace(text) ? "Cancel" : text;
    }

    private void OnPrimaryClicked(object? sender, EventArgs e) =>
        PrimaryClicked?.Invoke(this, e);

    private void OnSecondaryClicked(object? sender, EventArgs e)
    {
        SecondaryClicked?.Invoke(this, e);
        Dismissed?.Invoke(this, e);
    }

    private void OnScrimTapped(object? sender, TappedEventArgs e)
    {
        if (!DismissOnScrim) return;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}
