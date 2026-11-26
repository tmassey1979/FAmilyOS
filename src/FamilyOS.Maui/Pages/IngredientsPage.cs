using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Searchable shared ingredient catalog with family-defined entries.</summary>
public sealed class IngredientsPage : ContentPage
{
    private readonly MealsApiClient _api;
    private readonly SearchBar _search = new() { Placeholder = "Search ingredients and aliases…" };
    private readonly VerticalStackLayout _items = new() { Spacing = 8 };
    private readonly Label _status = new() { FontSize = 12 };
    private readonly Picker _category = new() { Title = "All categories" };
    private readonly Button _loadMore = new() { Text = "Load more ingredients", HorizontalOptions = LayoutOptions.Fill };
    private const int PageSize = 50;
    private int _skip;
    private CancellationTokenSource? _searchCts;

    public IngredientsPage(MealsApiClient api)
    {
        _api = api;
        Title = "Ingredients";
        var title = new Label { Text = "Ingredient catalog", FontSize = 28, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var subtitle = new Label { Text = "A shared vocabulary for recipes and shopping. Family additions appear alongside the built-in catalog.", FontSize = 13 };
        subtitle.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        foreach (var value in new[] { "All categories", "Produce", "Dairy", "Meat", "Seafood", "Pantry", "Spices", "Baking", "Frozen", "Other" }) _category.Items.Add(value);
        _category.SelectedIndex = 0;
        _category.SelectedIndexChanged += async (_, _) => await SearchAsync(_search.Text);
        _search.TextChanged += async (_, e) => await SearchAsync(e.NewTextValue);
        var add = new Button { Text = "＋ Add ingredient" };
        add.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        add.Clicked += async (_, _) => await AddAsync();
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _loadMore.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _loadMore.IsVisible = false;
        _loadMore.Clicked += async (_, _) => await SearchAsync(_search.Text, false, false);
        Content = new RefreshView
        {
            Command = new Command(async () => await SearchAsync(_search.Text, false)),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(18, 14, 18, 28), Spacing = 12,
                    Children = { title, subtitle, _search, _category, add, _status, _items, _loadMore }
                }
            }
        };
    }

    protected override async void OnAppearing() { base.OnAppearing(); if (_items.Children.Count == 0) await SearchAsync(null, false); }

    private async Task SearchAsync(string? term, bool debounce = true, bool reset = true)
    {
        _searchCts?.Cancel(); _searchCts?.Dispose(); _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        if (reset) _skip = 0;
        _loadMore.IsEnabled = false;
        _status.Text = "Searching ingredient catalog…";
        try
        {
            if (debounce) await Task.Delay(220, token);
            var category = _category.SelectedIndex <= 0 ? null : _category.SelectedItem?.ToString();
            var found = await _api.SearchIngredientsAsync(term ?? "", PageSize, token, category, _skip);
            if (token.IsCancellationRequested) return;
            if (reset) _items.Children.Clear();
            foreach (var ingredient in found)
            {
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
                var name = new Label { Text = ingredient.Name, FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalTextAlignment = TextAlignment.Center };
                name.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
                row.Add(name, 0, 0);
                row.Add(new Label { Text = ingredient.Category + (ingredient.IsBuiltIn ? " · BUILT-IN" : " · FAMILY"), FontSize = 10, VerticalTextAlignment = TextAlignment.Center }, 1, 0);
                var card = new Border { Padding = 12, Content = row }; card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
                _items.Children.Add(card);
                if (!string.IsNullOrWhiteSpace(ingredient.AliasesCsv))
                {
                    var aliases = new Label { Text = "Also known as: " + ingredient.AliasesCsv, FontSize = 11, Margin = new Thickness(12, -3, 0, 4) };
                    aliases.SetDynamicResource(VisualElement.StyleProperty, "TextCaption"); _items.Children.Add(aliases);
                }
            }
            if (reset && found.Count == 0) _items.Children.Add(new Label { Text = "No ingredients found. Add it to your family catalog.", FontSize = 13 });
            _skip += found.Count;
            _loadMore.IsVisible = found.Count == PageSize;
            _status.Text = $"{_skip} ingredient{(_skip == 1 ? "" : "s")} shown";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Ingredient catalog unavailable. Try again."; }
        finally { _loadMore.IsEnabled = true; }
    }

    private async Task AddAsync()
    {
        var name = new Entry { Placeholder = "Ingredient name" };
        var category = new Picker { Title = "Category" };
        foreach (var value in new[] { "Produce", "Dairy", "Meat", "Seafood", "Pantry", "Spices", "Baking", "Frozen", "Other" }) category.Items.Add(value);
        category.SelectedIndex = 8;
        var aliases = new Entry { Placeholder = "Aliases, separated by commas (optional)" };
        var save = new Button { Text = "Save ingredient" };
        save.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        var page = new ContentPage
        {
            Title = "Add ingredient",
            Content = new VerticalStackLayout
            {
                Padding = 20, Spacing = 12,
                Children = { new Label { Text = "Add to family catalog", FontSize = 22, FontAttributes = FontAttributes.Bold }, name, category, aliases, save }
            }
        };
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { await page.DisplayAlertAsync("Name required", "Enter an ingredient name.", "OK"); return; }
            try
            {
                await _api.CreateIngredientAsync(new CreateIngredientDto(name.Text.Trim(), category.SelectedItem?.ToString() ?? "Other", aliases.Text));
                await Navigation.PopModalAsync();
                _search.Text = name.Text.Trim();
                await SearchAsync(_search.Text, false);
            }
            catch (Exception ex) { await page.DisplayAlertAsync("Ingredient not saved", ex is FamilyApiException ? ex.Message : "Please try again.", "OK"); }
        };
        await Navigation.PushModalAsync(new NavigationPage(page));
    }
}
