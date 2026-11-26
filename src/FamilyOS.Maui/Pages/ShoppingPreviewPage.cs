using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Review the complete generated list before adding it to household shopping.</summary>
public sealed class ShoppingPreviewPage : ContentPage
{
    private readonly MealsApiClient _api;
    private readonly DateTime _weekStart;
    private readonly DateTime? _day;
    private readonly Button _add = new() { Text = "Add items to shopping list" };
    private readonly Label _status = new() { FontSize = 12 };
    private bool _saving;

    public ShoppingPreviewPage(MealsApiClient api, DateTime weekStart, DateTime? day, MealShoppingPreviewDto preview)
    {
        _api = api;
        _weekStart = weekStart;
        _day = day;
        Title = "Shopping list";
        _add.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _add.Clicked += async (_, _) => await AddToShoppingAsync();

        var heading = new Label { Text = day is null ? "Weekly shopping list" : $"{day.Value:dddd} shopping list", FontSize = 25, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var summary = new Label { Text = $"{preview.Items.Count} ingredients from your planned meals", FontSize = 13 };
        summary.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var items = new VerticalStackLayout { Spacing = 8 };
        if (preview.Items.Count == 0)
        {
            var empty = new Label { Text = "No ingredients are planned for this period yet.", FontSize = 14 };
            empty.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            items.Children.Add(empty);
        }
        else
        {
            for (var i = 0; i < preview.Items.Count; i++)
            {
                var icon = new Label { Text = "🛒", FontSize = 16, VerticalTextAlignment = TextAlignment.Center };
                var itemText = new Label { Text = preview.Items[i], FontSize = 14, VerticalTextAlignment = TextAlignment.Center };
                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 10
                };
                row.Add(icon, 0, 0);
                row.Add(itemText, 1, 0);
                var card = new Border { Padding = new Thickness(12, 9), Content = row };
                card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
                items.Children.Add(card);
            }
        }

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 14),
                Spacing = 12,
                Children = { heading, summary, items, _status }
            }
        };
        var footer = new VerticalStackLayout
        {
            Padding = new Thickness(18, 10),
            Spacing = 8,
            Children = { _add }
        };
        var layout = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
        };
        layout.Add(scroll, 0, 0);
        layout.Add(footer, 0, 1);
        Content = layout;
    }

    private async Task AddToShoppingAsync()
    {
        if (_saving) return;
        _saving = true;
        _add.IsEnabled = false;
        _status.Text = "Adding ingredients…";
        try
        {
            var result = _day is null
                ? await _api.AddWeekToShoppingAsync(_weekStart)
                : await _api.AddDayToShoppingAsync(_day.Value);
            await DisplayAlertAsync("Shopping list updated", $"Added {result.Added} new item(s) to household shopping.", "Done");
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex is FamilyApiException ? ex.Message : "Could not add these items. Try again.";
        }
        finally
        {
            _saving = false;
            _add.IsEnabled = true;
        }
    }
}
