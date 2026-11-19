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

public sealed class MealsPage() : FeaturePlaceholderPage(
    "Meals",
    "Weekly meal planning and recipes.",
    "menu_meals.png") { }

public sealed class ChatPage() : FeaturePlaceholderPage(
    "Chat",
    "Household conversations.",
    "menu_chat.png") { }

public sealed class SettingsPage() : FeaturePlaceholderPage(
    "Settings",
    "Account, household, security, and app preferences.",
    "menu_settings.png") { }

public sealed class OutcomesPage() : FeaturePlaceholderPage(
    "Outcomes",
    "A view of household activity and progress.",
    "menu_outcomes.png") { }

public sealed class MorePage() : FeaturePlaceholderPage(
    "More",
    "Additional FamilyOS destinations.",
    "menu_more.png")
{
    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (Content is not ScrollView scroll || scroll.Content is not VerticalStackLayout root)
            return;

        // Avoid duplicating rows on re-appear.
        if (root.Children.Count > 1)
            return;

        var list = new VerticalStackLayout { Spacing = 10 };
        AddDestination(list, "Requests", nameof(RequestsPage), "menu_requests.png");
        AddDestination(list, "Meals", nameof(MealsPage), "menu_meals.png");
        AddDestination(list, "Chat", nameof(ChatPage), "menu_chat.png");
        AddDestination(list, "Outcomes", nameof(OutcomesPage), "menu_outcomes.png");
        AddDestination(list, "Notifications", nameof(NotificationsPage), "menu_notifications.png");
        AddDestination(list, "Settings", nameof(SettingsPage), "menu_settings.png");
        AddDestination(list, "Design system", nameof(DesignSystemGalleryPage), "menu_gallery.png");
        root.Children.Add(list);
    }

    private static void AddDestination(VerticalStackLayout layout, string title, string route, string icon)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14,
            Padding = new Thickness(4, 2)
        };

        row.Add(new Image
        {
            Source = icon,
            WidthRequest = 24,
            HeightRequest = 24,
            VerticalOptions = LayoutOptions.Center
        }, 0);

        row.Add(new Label
        {
            Text = title,
            Style = GetStyle("TextBodyStrong"),
            VerticalOptions = LayoutOptions.Center
        }, 1);

        row.Add(new Label
        {
            Text = "›",
            Style = GetStyle("TextMutedArrow") ?? GetStyle("TextCaption"),
            VerticalOptions = LayoutOptions.Center,
            FontSize = 20
        }, 2);

        var border = new Border
        {
            Style = GetStyle("FoCardFrameStyle"),
            Padding = new Thickness(14, 12),
            Content = row
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Shell.Current.GoToAsync(route);
        border.GestureRecognizers.Add(tap);

        layout.Children.Add(border);
    }
}
