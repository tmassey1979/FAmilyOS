namespace FamilyOS.Maui.Controls;

public partial class FoSectionTitle : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FoSectionTitle), string.Empty,
            propertyChanged: (b, _, n) => ((FoSectionTitle)b).TitleLabel.Text = ((string)n).ToUpperInvariant());
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FoSectionTitle() => InitializeComponent();
}
