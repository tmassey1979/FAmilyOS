using FamilyOS.Maui.Services;
using Microsoft.Maui.Layouts;

namespace FamilyOS.Maui.Pages;

/// <summary>Dedicated family recipe library and discovery experience.</summary>
public sealed class RecipesPage : ContentPage
{
    private readonly MealsApiClient _api;
    private readonly VerticalStackLayout _items = new() { Spacing = 12 };
    private readonly SearchBar _search = new() { Placeholder = "Search recipes, ingredients, or cuisine…" };
    private readonly Button _more = new() { Text = "Load more recipes" };
    private readonly Label _status = new() { FontSize = 12 };
    private readonly Picker _sort = new() { Title = "Sort recipes", WidthRequest = 170 };
    private readonly HorizontalStackLayout _filters = new() { Spacing = 8 };
    private List<RecipeDto> _recipes = new();
    private List<RecipeMemberPreferenceDto> _preferences = new();
    private int _total;
    private string _filter = "All";
    private CancellationTokenSource? _searchCts;
    private const int Batch = 24;
    private bool _loading;

    public RecipesPage(MealsApiClient api)
    {
        _api = api;
        Title = "Recipes";
        var title = new Label { Text = "Recipe library", FontSize = 28, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var subtitle = new Label { Text = "Family favorites, new ideas, and recipes everyone can weigh in on.", FontSize = 13 };
        subtitle.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        var create = new Button { Text = "＋ Create a recipe" };
        create.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        create.Clicked += async (_, _) => await EditRecipeAsync();
        var import = new Button { Text = "Import from a website" };
        import.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        import.Clicked += async (_, _) => await ImportRecipeAsync();
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _more.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _more.Clicked += async (_, _) => await LoadMoreAsync();
        _search.TextChanged += async (_, e) => await SearchAsync(e.NewTextValue);
        foreach (var option in new[] { "Recommended", "Quickest", "A to Z" }) _sort.Items.Add(option);
        _sort.SelectedIndex = 0;
        _sort.SelectedIndexChanged += (_, _) => Render();
        foreach (var value in new[] { "All", "Favorites", "Dinner", "Breakfast", "Lunch", "Quick", "Vegetarian", "Kid-Friendly", "Budget-Friendly", "Family-Favorite", "High-Protein", "Slow-Cooker" })
        {
            var chip = new Button { Text = value, FontSize = 12, Padding = new Thickness(13, 7), CornerRadius = 18 };
            chip.SetDynamicResource(VisualElement.StyleProperty, value == _filter ? "FoPrimaryButtonStyle" : "FoSecondaryButtonStyle");
            chip.Clicked += async (_, _) =>
            {
                _filter = value;
                foreach (var button in _filters.Children.OfType<Button>())
                    button.SetDynamicResource(VisualElement.StyleProperty, button.Text == value ? "FoPrimaryButtonStyle" : "FoSecondaryButtonStyle");
                await SearchAsync(_search.Text);
            };
            _filters.Children.Add(chip);
        }
        Content = new RefreshView
        {
            Command = new Command(async () => await SearchAsync(_search.Text, false)),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(18, 14, 18, 28), Spacing = 12,
                    Children = { title, subtitle, create, import, _search,
                        new ScrollView { Orientation = ScrollOrientation.Horizontal, HeightRequest = 48, Content = _filters },
                        _sort, _status, _items, _more }
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_recipes.Count == 0 && !_loading) await SearchAsync(_search.Text, false);
    }

    private async Task SearchAsync(string? term, bool debounce = true)
    {
        _searchCts?.Cancel(); _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        var query = string.IsNullOrWhiteSpace(term) ? null : term.Trim();
        var tag = _filter is "All" or "Favorites" ? null : _filter;
        var favorites = _filter == "Favorites";
        _more.IsEnabled = false;
        _status.Text = "Searching the recipe collection…";
        try
        {
            if (debounce) await Task.Delay(250, token);
            var page = await _api.GetRecipesPageAsync(0, Batch, token, query, tag, favorites);
            var preferences = await _api.GetRecipePreferencesAsync(page.Items.Select(x => x.Id), token);
            if (token.IsCancellationRequested) return;
            _recipes = page.Items; _preferences = preferences; _total = page.TotalCount;
            Render();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Recipes could not be loaded. Try again."; }
        finally { if (!token.IsCancellationRequested) _more.IsEnabled = true; }
    }

    private async Task LoadMoreAsync()
    {
        if (_loading || _recipes.Count >= _total) return;
        _loading = true; _more.IsEnabled = false;
        var token = _searchCts?.Token ?? CancellationToken.None;
        try
        {
            var page = await _api.GetRecipesPageAsync(_recipes.Count, Batch, token,
                string.IsNullOrWhiteSpace(_search.Text) ? null : _search.Text.Trim(),
                _filter is "All" or "Favorites" ? null : _filter, _filter == "Favorites");
            if (token.IsCancellationRequested) return;
            var prefs = await _api.GetRecipePreferencesAsync(page.Items.Select(x => x.Id), token);
            _recipes.AddRange(page.Items); _preferences.AddRange(prefs); _total = page.TotalCount; Render();
        }
        catch (Exception ex) { await DisplayAlertAsync("Recipes unavailable", ex is FamilyApiException ? ex.Message : "Could not load more recipes.", "OK"); }
        finally { _loading = false; _more.IsEnabled = true; }
    }

    private void Render()
    {
        _items.Children.Clear();
        var ordered = _sort.SelectedIndex switch
        {
            1 => _recipes.OrderBy(x => (x.PrepMinutes ?? 999) + (x.CookMinutes ?? 999)).ThenBy(x => x.Name),
            2 => _recipes.OrderBy(x => x.Name),
            _ => _recipes.OrderByDescending(x => _preferences.Count(p => p.RecipeId == x.Id && p.IsFavorite))
                .ThenByDescending(x => _preferences.Count(p => p.RecipeId == x.Id && p.Preference == "Like"))
                .ThenBy(x => x.Name)
        };
        foreach (var recipe in ordered) _items.Children.Add(BuildCard(recipe));
        _status.Text = $"{_total:N0} recipes · showing {_recipes.Count}";
        _more.IsVisible = _recipes.Count < _total;
        _more.Text = $"Load more recipes ({Math.Max(0, _total - _recipes.Count)} remaining)";
        if (_recipes.Count == 0) _items.Children.Add(Empty("No recipes found", "Try another search or category."));
    }

    private View BuildCard(RecipeDto recipe)
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        if (Uri.TryCreate(recipe.ImageUrl, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http"))
            stack.Children.Add(new Image { Source = ImageSource.FromUri(uri), HeightRequest = 180, Aspect = Aspect.AspectFill });
        var name = new Label { Text = recipe.Name, FontSize = 17, FontAttributes = FontAttributes.Bold };
        name.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong"); stack.Children.Add(name);
        stack.Children.Add(new Label { Text = $"{(recipe.IsBuiltIn ? "FAMILYOS COLLECTION" : "FAMILY RECIPE")} · {recipe.Servings} servings" +
            (recipe.PrepMinutes is int prep ? $" · {prep} min prep" : "") + (recipe.CookMinutes is int cook ? $" · {cook} min cook" : "") +
            (string.IsNullOrWhiteSpace(recipe.Cuisine) ? "" : $" · {recipe.Cuisine}"), FontSize = 10 });
        if (!string.IsNullOrWhiteSpace(recipe.Description)) stack.Children.Add(new Label { Text = recipe.Description, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap });
        if (recipe.Tags is { Count: > 0 }) stack.Children.Add(new Label { Text = string.Join(" · ", recipe.Tags), FontSize = 11 });
        var memberPrefs = _preferences.Where(p => p.RecipeId == recipe.Id).ToList();
        var names = new List<string>();
        var favs = memberPrefs.Where(p => p.IsFavorite).Select(p => p.MemberName).ToArray();
        var likes = memberPrefs.Where(p => p.Preference == "Like").Select(p => p.MemberName).ToArray();
        var dislikes = memberPrefs.Where(p => p.Preference == "Dislike").Select(p => p.MemberName).ToArray();
        if (favs.Length > 0) names.Add("♥ " + string.Join(", ", favs));
        if (likes.Length > 0) names.Add("👍 " + string.Join(", ", likes));
        if (dislikes.Length > 0) names.Add("Not for: " + string.Join(", ", dislikes));
        if (names.Count > 0) stack.Children.Add(new Label { Text = string.Join("  ·  ", names), FontSize = 11 });
        var mine = memberPrefs.FirstOrDefault(p => p.IsCurrentMember);
        var feedback = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row };
        var favorite = new Button { Text = mine?.IsFavorite == true ? "♥ Favorite" : "♡ Favorite", FontSize = 11 };
        favorite.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        favorite.Clicked += async (_, _) => await SavePreferenceAsync(recipe, mine?.Preference, !(mine?.IsFavorite ?? false));
        var like = new Button { Text = mine?.Preference == "Like" ? "✓ Liked" : "👍 Like", FontSize = 11 };
        like.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        like.Clicked += async (_, _) => await SavePreferenceAsync(recipe, mine?.Preference == "Like" ? null : "Like", mine?.IsFavorite ?? false);
        var dislike = new Button { Text = mine?.Preference == "Dislike" ? "✓ Not for me" : "👎 Not for me", FontSize = 11 };
        dislike.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        dislike.Clicked += async (_, _) => await SavePreferenceAsync(recipe, mine?.Preference == "Dislike" ? null : "Dislike", mine?.IsFavorite ?? false);
        feedback.Children.Add(favorite); feedback.Children.Add(like); feedback.Children.Add(dislike); stack.Children.Add(feedback);
        var actions = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row };
        var details = new Button { Text = "View recipe", FontSize = 12 };
        details.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        details.Clicked += async (_, _) => await ShowDetailsAsync(recipe);
        var apply = new Button { Text = "Add to meal plan", FontSize = 12 };
        apply.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        apply.Clicked += async (_, _) => await ApplyAsync(recipe);
        var shop = new Button { Text = "Add ingredients to shopping", FontSize = 12 };
        shop.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        shop.Clicked += async (_, _) => await AddToShoppingAsync(recipe);
        actions.Children.Add(details); actions.Children.Add(apply); actions.Children.Add(shop);
        if (!recipe.IsBuiltIn)
        {
            var delete = new Button { Text = "Delete", FontSize = 11 };
            delete.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            delete.Clicked += async (_, _) => await DeleteAsync(recipe);
            actions.Children.Add(delete);
        }
        stack.Children.Add(actions);
        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return card;
    }

    private async Task ShowDetailsAsync(RecipeDto recipe)
    {
        try
        {
            var details = await _api.GetRecipeDetailsAsync(recipe.Id);
            var ingredientLines = details.Ingredients.Select(i => $"{i.Quantity?.ToString() ?? ""} {i.Unit} {i.Name} {i.PreparationNote}".Trim());
            var steps = details.Steps.OrderBy(s => s.StepNumber).Select(s => $"{s.StepNumber}. {s.Text}");
            var body = new VerticalStackLayout { Spacing = 10, Padding = 16 };
            body.Children.Add(new Label { Text = details.Name, FontSize = 24, FontAttributes = FontAttributes.Bold });
            body.Children.Add(new Label { Text = $"{details.Servings} servings · {details.PrepMinutes ?? 0} min prep · {details.CookMinutes ?? 0} min cook" });
            body.Children.Add(new Label { Text = "INGREDIENTS", FontAttributes = FontAttributes.Bold });
            foreach (var line in ingredientLines) body.Children.Add(new Label { Text = "• " + line, FontSize = 13 });
            body.Children.Add(new Label { Text = "DIRECTIONS", FontAttributes = FontAttributes.Bold });
            foreach (var line in steps) body.Children.Add(new Label { Text = line, FontSize = 13 });
            if (!string.IsNullOrWhiteSpace(details.LegacyIngredientsCsv)) body.Children.Add(new Label { Text = details.LegacyIngredientsCsv });
            await Navigation.PushModalAsync(new NavigationPage(new ContentPage { Title = recipe.Name, Content = new ScrollView { Content = body } }));
        }
        catch (Exception ex) { await DisplayAlertAsync("Recipe unavailable", ex is FamilyApiException ? ex.Message : "Could not open this recipe.", "OK"); }
    }

    private async Task SavePreferenceAsync(RecipeDto recipe, string? preference, bool favorite)
    {
        try { await _api.SetRecipePreferenceAsync(recipe.Id, new RecipeMemberPreferenceWriteDto(preference, favorite)); await SearchAsync(_search.Text, false); }
        catch (Exception ex) { await DisplayAlertAsync("Preference not saved", ex is FamilyApiException ? ex.Message : "Please try again.", "OK"); }
    }

    private async Task EditRecipeAsync()
    {
        await Navigation.PushModalAsync(new NavigationPage(new RecipeEditorPage(_api)));
        await SearchAsync(_search.Text, false);
    }

    private async Task ImportRecipeAsync()
    {
        var url = await DisplayPromptAsync("Import recipe", "Paste a recipe webpage URL", "Continue", "Cancel", "https://…", keyboard: Keyboard.Url);
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            var preview = await _api.ImportRecipePreviewAsync(url.Trim());
            var ingredients = string.Join(Environment.NewLine, preview.Ingredients);
            var steps = string.Join(Environment.NewLine, preview.Steps.Select((s, i) => $"{i + 1}. {s}"));
            if (await DisplayAlertAsync($"Import {preview.Name}?", $"{preview.Servings} servings\n\nIngredients:\n{ingredients}\n\nSteps:\n{steps}", "Create recipe", "Cancel"))
            {
                await _api.CreateRecipeAsync(new RecipeWriteDto(preview.Name, preview.Servings, ingredients, steps));
                await SearchAsync(_search.Text, false);
            }
        }
        catch (Exception ex) { await DisplayAlertAsync("Import unavailable", ex is FamilyApiException ? ex.Message : "Could not import that recipe.", "OK"); }
    }

    private async Task ApplyAsync(RecipeDto recipe)
    {
        var start = DateTime.UtcNow.Date;
        start = start.AddDays(-(((int)start.DayOfWeek + 6) % 7));
        var choices = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToArray();
        var dayLabels = choices.Select(d => d.ToString("ddd, MMM d")).ToArray();
        var day = await DisplayActionSheetAsync("Add to which day?", "Cancel", null, dayLabels);
        var index = Array.IndexOf(dayLabels, day);
        if (index < 0) return;
        var slot = await DisplayActionSheetAsync("Choose a meal", "Cancel", null, "Breakfast", "Lunch", "Dinner", "Snack");
        if (string.IsNullOrWhiteSpace(slot) || slot == "Cancel") return;
        try
        {
            await _api.ApplyRecipeAsync(recipe.Id, new ApplyRecipeDto(choices[index], slot, start));
            await DisplayAlertAsync("Added to meal plan", $"{recipe.Name} was added as a dish. It won't replace other dishes.", "OK");
        }
        catch (Exception ex) { await DisplayAlertAsync("Could not add recipe", ex is FamilyApiException ? ex.Message : "Please try again.", "OK"); }
    }

    private async Task AddToShoppingAsync(RecipeDto recipe)
    {
        var servings = await DisplayPromptAsync("Shopping list", "How many servings?", initialValue: recipe.Servings.ToString(), keyboard: Keyboard.Numeric);
        if (servings is null) return;
        int? target = int.TryParse(servings, out var value) && value > 0 ? value : null;
        try
        {
            var result = await _api.AddRecipeToShoppingAsync(recipe.Id, target);
            await DisplayAlertAsync("Shopping list updated", $"Added {result.Added} new item(s).", "OK");
        }
        catch (Exception ex) { await DisplayAlertAsync("Shopping list not updated", ex is FamilyApiException ? ex.Message : "Please try again.", "OK"); }
    }

    private async Task DeleteAsync(RecipeDto recipe)
    {
        if (!await DisplayAlertAsync("Delete recipe?", $"Delete {recipe.Name}?", "Delete", "Keep")) return;
        try { await _api.DeleteRecipeAsync(recipe.Id); await SearchAsync(_search.Text, false); }
        catch (Exception ex) { await DisplayAlertAsync("Could not delete recipe", ex is FamilyApiException ? ex.Message : "Please try again.", "OK"); }
    }

    private static View Empty(string title, string message)
    {
        var layout = new VerticalStackLayout { Spacing = 6, Padding = 14, Children = { new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold }, new Label { Text = message, FontSize = 12 } } };
        var border = new Border { Padding = 12, Content = layout }; border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle"); return border;
    }
}
