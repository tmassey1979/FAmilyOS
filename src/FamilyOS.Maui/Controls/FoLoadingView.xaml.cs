namespace FamilyOS.Maui.Controls;

public partial class FoLoadingView : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(FoLoadingView), "Loading…",
            propertyChanged: (b, _, n) => ((FoLoadingView)b).MessageLabel.Text = (string)n);
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public FoLoadingView()
    {
        InitializeComponent();
        SemanticProperties.SetDescription(this, "Loading");
    }
}
