namespace FamilyOS.Maui.Pages;

/// <summary>
/// Temporary, deliberately data-free destinations that establish the MAUI
/// navigation map before each feature is migrated. Replace each page in its
/// feature issue; do not add mock household data here.
/// </summary>
public abstract class FeaturePlaceholderPage : ContentPage
{
    protected FeaturePlaceholderPage(string title, string description, string? icon = null)
    {
        Title = title;

        var heading = new Label
        {
            Text = title,
            Style = GetStyle("TextDisplay")
        };

        var explanation = new Label
        {
            Text = description,
            LineBreakMode = LineBreakMode.WordWrap,
            Style = GetStyle("TextBody")
        };

        var status = new Label
        {
            Text = "Wired for navigation. Feature UI lands with its migration issue.",
            LineBreakMode = LineBreakMode.WordWrap,
            Style = GetStyle("TextCaption")
        };

        View header;
        if (!string.IsNullOrWhiteSpace(icon))
        {
            header = new HorizontalStackLayout
            {
                Spacing = 12,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Image
                    {
                        Source = icon,
                        WidthRequest = 28,
                        HeightRequest = 28,
                        VerticalOptions = LayoutOptions.Center
                    },
                    heading
                }
            };
        }
        else
        {
            header = heading;
        }

        var card = new Border
        {
            Style = GetStyle("FoCardFrameStyle"),
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children = { header, explanation, status }
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 24),
                Spacing = 16,
                Children = { card }
            }
        };
    }

    protected static Style? GetStyle(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style)
            return style;
        return null;
    }
}
