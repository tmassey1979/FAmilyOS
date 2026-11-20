namespace FamilyOS.Maui.Controls;

public partial class FoListRow : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FoListRow), string.Empty,
            propertyChanged: (b, _, n) => ((FoListRow)b).TitleLabel.Text = (string)n);
    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(FoListRow), string.Empty,
            propertyChanged: (b, _, n) =>
            {
                var c = (FoListRow)b;
                var t = (string)n;
                c.SubtitleLabel.Text = t;
                c.SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(t);
            });
    public static readonly BindableProperty TrailingProperty =
        BindableProperty.Create(nameof(Trailing), typeof(View), typeof(FoListRow), null,
            propertyChanged: (b, _, n) => ((FoListRow)b).TrailingHost.Content = n as View);
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public View? Trailing { get => (View?)GetValue(TrailingProperty); set => SetValue(TrailingProperty, value); }
    public FoListRow() => InitializeComponent();
}
