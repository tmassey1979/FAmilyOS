namespace FamilyOS.Maui.Controls;

public partial class FoEmptyState : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FoEmptyState), string.Empty,
            propertyChanged: (b, _, n) => ((FoEmptyState)b).TitleLabel.Text = (string)n);
    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(FoEmptyState), string.Empty,
            propertyChanged: (b, _, n) =>
            {
                var c = (FoEmptyState)b;
                var t = (string)n;
                c.SubtitleLabel.Text = t;
                c.SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(t);
            });
    public static readonly BindableProperty ActionProperty =
        BindableProperty.Create(nameof(Action), typeof(View), typeof(FoEmptyState), null,
            propertyChanged: (b, _, n) =>
            {
                var c = (FoEmptyState)b;
                c.ActionHost.Content = n as View;
                c.ActionHost.IsVisible = n is not null;
            });
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public View? Action { get => (View?)GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    public FoEmptyState() => InitializeComponent();
}
