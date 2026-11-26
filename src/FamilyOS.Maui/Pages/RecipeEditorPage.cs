using System.Globalization;
using System.Text.RegularExpressions;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Fast, catalog-first recipe editor. Select ingredients, then enter quantities and units.</summary>
public sealed class RecipeEditorPage : ContentPage
{
    private readonly MealsApiClient _api;
    private readonly Entry _name = new() { Placeholder = "Recipe name" };
    private readonly Entry _servings = new() { Text = "4", Keyboard = Keyboard.Numeric, WidthRequest = 90 };
    private readonly Editor _description = new() { Placeholder = "A short description (optional)", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 70 };
    private string? _imageUrl;
    private string? _sourceUrl;
    private string? _cuisine;
    private int? _prepMinutes;
    private int? _cookMinutes;
    private readonly SearchBar _search = new() { Placeholder = "Find an ingredient…" };
    private readonly VerticalStackLayout _results = new() { Spacing = 6 };
    private readonly VerticalStackLayout _selected = new() { Spacing = 6 };
    private readonly Editor _steps = new() { Placeholder = "One preparation step per line", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 120 };
    private readonly Entry _tags = new() { Placeholder = "Dinner, Quick, Kid-Friendly…" };
    private readonly Label _status = new() { FontSize = 12 };
    private readonly Button _save = new() { Text = "Save recipe" };
    private readonly List<SelectedIngredient> _ingredients = new();
    private CancellationTokenSource? _searchCts;
    private bool _saving;

    public RecipeEditorPage(MealsApiClient api)
    {
        _api = api;
        Title = "Create recipe";
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _save.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _save.Clicked += async (_, _) => await SaveAsync();
        _search.TextChanged += async (_, e) => await SearchAsync(e.NewTextValue);

        var heading = new Label { Text = "Build a recipe", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var import = new Button { Text = "Import recipe from website", FontSize = 12 };
        import.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        import.Clicked += async (_, _) => await ImportFromWebsiteAsync();

        var servingsRow = new HorizontalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "Servings", VerticalTextAlignment = TextAlignment.Center },
                _servings
            }
        };
        var addCustom = new Button { Text = "＋ Add ingredient not listed", FontSize = 12 };
        addCustom.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        addCustom.Clicked += async (_, _) => await AddCustomIngredientAsync();

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 14),
            Spacing = 12,
            Children =
            {
                heading,
                import,
                _name,
                _description,
                servingsRow,
                new Label { Text = "INGREDIENTS", FontAttributes = FontAttributes.Bold, FontSize = 12 },
                _search,
                _results,
                addCustom,
                new Label { Text = "SELECTED INGREDIENTS", FontAttributes = FontAttributes.Bold, FontSize = 12 },
                _selected,
                new Label { Text = "PREPARATION", FontAttributes = FontAttributes.Bold, FontSize = 12 },
                _steps,
                new Label { Text = "DISCOVERY TAGS", FontAttributes = FontAttributes.Bold, FontSize = 12 },
                _tags,
                _status,
                _save
            }
        };
        Content = new ScrollView { Content = content };
    }

    private async Task SearchAsync(string? term)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _results.Children.Clear();
        if (string.IsNullOrWhiteSpace(term))
            return;
        try
        {
            await Task.Delay(250, token);
            var matches = await _api.SearchIngredientsAsync(term.Trim(), 15, token);
            if (token.IsCancellationRequested) return;
            _results.Children.Clear();
            foreach (var item in matches)
            {
                var button = new Button
                {
                    Text = $"{item.Name}   ·   {item.Category}",
                    FontSize = 13,
                    HorizontalOptions = LayoutOptions.Fill,
                    HorizontalTextAlignment = TextAlignment.Start
                };
                button.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
                button.Clicked += async (_, _) => await SelectIngredientAsync(item);
                _results.Children.Add(button);
            }
            if (matches.Count == 0)
            {
                var empty = new Label { Text = "No match. Add it to your family catalog below.", FontSize = 12 };
                empty.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
                _results.Children.Add(empty);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _status.Text = ex is FamilyApiException ? ex.Message : "Ingredient search is temporarily unavailable.";
        }
    }

    private async Task ImportFromWebsiteAsync()
    {
        var url = await DisplayPromptAsync("Import recipe", "Paste the recipe webpage URL", maxLength: 1000);
        if (string.IsNullOrWhiteSpace(url)) return;
        _status.Text = "Reading recipe page…";
        try
        {
            var preview = await _api.ImportRecipePreviewAsync(url.Trim());
            _name.Text = preview.Name;
            _servings.Text = preview.Servings.ToString(CultureInfo.InvariantCulture);
            _description.Text = preview.Description ?? string.Empty;
            _steps.Text = string.Join(Environment.NewLine, preview.Steps);
            _tags.Text = string.Join(", ", preview.Tags);
            _imageUrl = preview.ImageUrl;
            _sourceUrl = preview.SourceUrl;
            _prepMinutes = preview.PrepMinutes;
            _cookMinutes = preview.CookMinutes;
            _cuisine = preview.Tags.FirstOrDefault(x =>
                new[] { "Italian", "Mexican", "American", "Asian", "Mediterranean" }
                    .Any(cuisine => x.Contains(cuisine, StringComparison.OrdinalIgnoreCase)));

            var added = 0;
            foreach (var rawLine in preview.Ingredients)
            {
                var (quantity, unit, ingredientName) = ParseImportedIngredient(rawLine);
                var matches = await _api.SearchIngredientsAsync(ingredientName, 8);
                var item = matches.FirstOrDefault(x => string.Equals(x.Name, ingredientName, StringComparison.OrdinalIgnoreCase))
                    ?? matches.FirstOrDefault();
                if (item is null)
                    item = await _api.CreateIngredientAsync(new CreateIngredientDto(ingredientName, "Other"));
                if (_ingredients.Any(x => x.Item.Id == item.Id)) continue;
                _ingredients.Add(new SelectedIngredient(item, quantity, unit, null));
                added++;
            }
            RenderSelected();
            _status.Text = $"Imported draft · {added} ingredients matched to your catalog. Review everything before saving.";
        }
        catch (Exception ex)
        {
            _status.Text = ex is FamilyApiException ? ex.Message : "Could not import this page. You can still enter the recipe manually.";
        }
    }

    private static (decimal? Quantity, string? Unit, string Name) ParseImportedIngredient(string raw)
    {
        var match = Regex.Match(raw.Trim(), @"^(?<quantity>\d+(?:\.\d+)?|\d+/\d+)\s+(?<unit>[A-Za-z]+)\s+(?<name>.+)$");
        if (!match.Success) return (null, null, raw.Trim());
        var quantityText = match.Groups["quantity"].Value;
        decimal quantity;
        if (quantityText.Contains('/'))
        {
            var parts = quantityText.Split('/');
            quantity = decimal.Parse(parts[0], CultureInfo.InvariantCulture) / decimal.Parse(parts[1], CultureInfo.InvariantCulture);
        }
        else quantity = decimal.Parse(quantityText, CultureInfo.InvariantCulture);
        return (quantity, match.Groups["unit"].Value.ToLowerInvariant(), match.Groups["name"].Value.Trim());
    }

    private async Task SelectIngredientAsync(IngredientCatalogDto item)
    {
        if (_ingredients.Any(x => x.Item.Id == item.Id))
        {
            await DisplayAlertAsync("Already added", $"{item.Name} is already in this recipe.", "OK");
            return;
        }

        var quantityText = await DisplayPromptAsync(item.Name, "Quantity (for example 1, 1/2, or 1.5). Leave blank if not needed.",
            placeholder: "1", maxLength: 12);
        if (quantityText is null) return;
        decimal? quantity = null;
        if (!string.IsNullOrWhiteSpace(quantityText))
        {
            if (!TryParseQuantity(quantityText, out var parsed) || parsed < 0)
            {
                await DisplayAlertAsync("Check quantity", "Enter a number such as 1, 0.5, or 1.5.", "OK");
                return;
            }
            quantity = parsed;
        }
        var unit = await DisplayPromptAsync(item.Name, "Unit (cup, tbsp, oz, lb, each…)", placeholder: "each", maxLength: 30);
        if (unit is null) return;
        var note = await DisplayPromptAsync(item.Name, "Preparation note (optional)", placeholder: "diced, softened…", maxLength: 100);
        if (note is null) return;
        _ingredients.Add(new SelectedIngredient(item, quantity, string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(), string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
        RenderSelected();
        _search.Text = string.Empty;
        _results.Children.Clear();
    }

    private async Task AddCustomIngredientAsync()
    {
        var name = await DisplayPromptAsync("New ingredient", "Ingredient name", maxLength: 160);
        if (string.IsNullOrWhiteSpace(name)) return;
        var category = await DisplayActionSheetAsync("Ingredient category", "Cancel", null,
            "Produce", "Meat", "Seafood", "Dairy", "Bakery", "Pantry", "Spices", "Canned", "Frozen", "Other");
        if (string.IsNullOrWhiteSpace(category) || category == "Cancel") return;
        try
        {
            var item = await _api.CreateIngredientAsync(new CreateIngredientDto(name.Trim(), category));
            await SelectIngredientAsync(item);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ingredient not added", ex is FamilyApiException ? ex.Message : "Could not add this ingredient.", "OK");
        }
    }

    private void RenderSelected()
    {
        _selected.Children.Clear();
        if (_ingredients.Count == 0)
        {
            var hint = new Label { Text = "Your ingredients will appear here as you add them.", FontSize = 12 };
            hint.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            _selected.Children.Add(hint);
            return;
        }

        for (var i = 0; i < _ingredients.Count; i++)
        {
            var index = i;
            var item = _ingredients[i];
            var amount = item.Quantity is null ? "" : item.Quantity.Value.ToString(CultureInfo.InvariantCulture) + " ";
            var detail = amount + (item.Unit ?? "") + (string.IsNullOrWhiteSpace(item.Note) ? "" : " · " + item.Note);
            var label = new Label { Text = $"{i + 1}. {item.Item.Name}\n{detail}", FontSize = 13, VerticalTextAlignment = TextAlignment.Center };
            label.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            var remove = new Button { Text = "Remove", FontSize = 11, Padding = new Thickness(10, 5) };
            remove.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            remove.Clicked += (_, _) => { _ingredients.RemoveAt(index); RenderSelected(); };
            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                Children = { label, remove }
            };
            Grid.SetColumn(remove, 1);
            var card = new Border { Padding = 10, Content = row };
            card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
            _selected.Children.Add(card);
        }
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            await DisplayAlertAsync("Recipe name", "Give this recipe a name first.", "OK");
            return;
        }
        if (!int.TryParse(_servings.Text, out var servings) || servings is < 1 or > 100)
        {
            await DisplayAlertAsync("Servings", "Enter a serving count between 1 and 100.", "OK");
            return;
        }
        if (_ingredients.Count == 0)
        {
            await DisplayAlertAsync("Ingredients", "Add at least one ingredient.", "OK");
            return;
        }

        _saving = true;
        _save.IsEnabled = false;
        _status.Text = "Saving recipe…";
        try
        {
            var ingredients = _ingredients.Select((x, i) => new StructuredRecipeIngredientWriteDto(
                x.Item.Id, x.Quantity, x.Unit, x.Note, null, i)).ToList();
            var steps = (_steps.Text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => new StructuredRecipeStepWriteDto(x)).ToList();
            var tags = (_tags.Text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            await _api.SaveStructuredRecipeAsync(new StructuredRecipeWriteDto(null, _name.Text.Trim(), servings, ingredients, steps, tags, _description.Text, _imageUrl, _sourceUrl, _prepMinutes, _cookMinutes, _cuisine));
            await DisplayAlertAsync("Recipe saved", "Your recipe is now in the family recipe library.", "Done");
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex is FamilyApiException ? ex.Message : "Could not save this recipe. Try again.";
        }
        finally
        {
            _saving = false;
            _save.IsEnabled = true;
        }
    }

    private static bool TryParseQuantity(string input, out decimal value)
    {
        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return true;
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var whole)
            && TryFraction(parts[1], out var fraction))
        {
            value = whole + fraction;
            return true;
        }
        return TryFraction(input.Trim(), out value);
    }

    private static bool TryFraction(string input, out decimal value)
    {
        value = 0;
        var parts = input.Split('/');
        if (parts.Length != 2
            || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var numerator)
            || !decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var denominator)
            || denominator == 0) return false;
        value = numerator / denominator;
        return true;
    }

    private sealed record SelectedIngredient(IngredientCatalogDto Item, decimal? Quantity, string? Unit, string? Note);
}
