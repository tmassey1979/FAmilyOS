namespace FamilyOS.Maui.Controls;

public partial class FoErrorView : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(FoErrorView), "Something went wrong.",
            propertyChanged: (b, _, n) => ((FoErrorView)b).MessageLabel.Text = (string)n);
    public event EventHandler? RetryClicked;
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public FoErrorView()
    {
        InitializeComponent();
        MessageLabel.Text = Message;
    }
    private void OnRetry(object? sender, EventArgs e) => RetryClicked?.Invoke(this, e);
}
