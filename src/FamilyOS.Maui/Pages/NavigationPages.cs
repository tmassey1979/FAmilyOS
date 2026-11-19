namespace FamilyOS.Maui.Pages;

/// <summary>
/// Temporary, deliberately data-free destinations that establish the MAUI
/// navigation map before each feature is migrated. Replace each page in its
/// feature issue; do not add mock household data here.
/// </summary>
public abstract class FeaturePlaceholderPage : ContentPage
{
    protected FeaturePlaceholderPage(string title, string description)
    {
        Title = title;

        var heading = new Label { Text = title, FontAttributes = FontAttributes.Bold, FontSize = 28 };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");

        var explanation = new Label
        {
            Text = description,
            FontSize = 16,
            LineBreakMode = LineBreakMode.WordWrap
        };
        explanation.SetDynamicResource(VisualElement.StyleProperty, "TextBody");

        var status = new Label
        {
            Text = "This destination is wired into navigation. Its real API-backed screen is part of the feature migration backlog.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(24),
            Spacing = 16,
            Children = { heading, explanation, status }
        };

        Content = new ScrollView { Content = content };
    }
}

public sealed class HomePage() : FeaturePlaceholderPage(
    "Family Pulse",
    "A clear view of what needs attention, what is coming up, and the next useful action.") { }

public sealed class TasksPage() : FeaturePlaceholderPage(
    "Tasks",
    "Household tasks, assignments, routines, and progress.") { }

public sealed class CalendarPage() : FeaturePlaceholderPage(
    "Calendar",
    "Family events and shared schedules.") { }

public sealed class FamilyPage() : FeaturePlaceholderPage(
    "Family",
    "Household members, roles, and shared coordination.") { }

public sealed class RequestsPage() : FeaturePlaceholderPage(
    "Requests",
    "Submit, review, and track household requests.") { }

public sealed class MealsPage() : FeaturePlaceholderPage(
    "Meals",
    "Weekly meal planning and recipes.") { }

public sealed class ChatPage() : FeaturePlaceholderPage(
    "Chat",
    "Household conversations.") { }

public sealed class SettingsPage() : FeaturePlaceholderPage(
    "Settings",
    "Account, household, security, and app preferences.") { }

public sealed class OutcomesPage() : FeaturePlaceholderPage(
    "Outcomes",
    "A view of household activity and progress.") { }

public sealed class NotificationsPage() : FeaturePlaceholderPage(
    "Notifications",
    "Updates and actions that need your attention.") { }

public sealed class MorePage() : FeaturePlaceholderPage(
    "More",
    "Additional FamilyOS destinations.")
{
    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (Content is not ScrollView scroll || scroll.Content is not VerticalStackLayout layout)
            return;

        if (layout.Children.Count > 3)
            return;

        AddDestination(layout, "Requests", nameof(RequestsPage));
        AddDestination(layout, "Meals", nameof(MealsPage));
        AddDestination(layout, "Chat", nameof(ChatPage));
        AddDestination(layout, "Outcomes", nameof(OutcomesPage));
        AddDestination(layout, "Notifications", nameof(NotificationsPage));
        AddDestination(layout, "Settings", nameof(SettingsPage));
        AddDestination(layout, "Design system gallery", nameof(DesignSystemGalleryPage));
    }

    private static void AddDestination(VerticalStackLayout layout, string title, string route)
    {
        var button = new Button { Text = title, HorizontalOptions = LayoutOptions.Fill };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        button.Clicked += async (_, _) => await Shell.Current.GoToAsync(route);
        layout.Children.Add(button);
    }
}
