namespace FamilyOS.Maui.Controls;

public partial class FoTextField : ContentView
{
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(FoTextField), string.Empty,
            propertyChanged: (b, _, n) => ((FoTextField)b).LabelText.Text = (string)n);
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FoTextField), string.Empty, BindingMode.TwoWay,
            propertyChanged: (b, _, n) =>
            {
                var c = (FoTextField)b;
                if (c.InnerEntry.Text != (string)n) c.InnerEntry.Text = (string)n;
            });
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(FoTextField), string.Empty,
            propertyChanged: (b, _, n) => ((FoTextField)b).InnerEntry.Placeholder = (string)n);
    public static readonly BindableProperty HelperTextProperty =
        BindableProperty.Create(nameof(HelperText), typeof(string), typeof(FoTextField), string.Empty,
            propertyChanged: (b, _, _) => ((FoTextField)b).ApplyHelper());
    public static readonly BindableProperty IsErrorProperty =
        BindableProperty.Create(nameof(IsError), typeof(bool), typeof(FoTextField), false,
            propertyChanged: (b, _, _) => ((FoTextField)b).ApplyHelper());
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string HelperText { get => (string)GetValue(HelperTextProperty); set => SetValue(HelperTextProperty, value); }
    public bool IsError { get => (bool)GetValue(IsErrorProperty); set => SetValue(IsErrorProperty, value); }
    public FoTextField() => InitializeComponent();
    private void OnTextChanged(object? sender, TextChangedEventArgs e) => Text = e.NewTextValue ?? string.Empty;
    private void ApplyHelper()
    {
        var has = !string.IsNullOrWhiteSpace(HelperText);
        HelperLabel.IsVisible = has;
        HelperLabel.Text = HelperText;
        HelperLabel.TextColor = IsError
            ? (Color)Application.Current!.Resources["AccentDanger"]
            : (Color)Application.Current!.Resources["TextMuted"];
    }
}
