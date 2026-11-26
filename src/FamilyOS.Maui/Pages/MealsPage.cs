using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Weekly household meal planner and reusable recipe collection.</summary>
public sealed class MealsPage : ContentPage
{
    private static readonly string[] Slots = ["Breakfast", "Lunch", "Dinner", "Snack"];
    private readonly MealsApiClient _api;
    private readonly VerticalStackLayout _planItems = new() { Spacing = 12 };
    private readonly VerticalStackLayout _recipeItems = new() { Spacing = 10 };
    private readonly SearchBar _recipeSearch = new() { Placeholder = "Search meals, ingredients, or cuisine…", FontSize = 14 };
    private readonly HorizontalStackLayout _recipeFilters = new() { Spacing = 8 };
    private List<RecipeDto> _allRecipes = new();
    private string _activeRecipeFilter = "All";
    private readonly Label _status = new() { FontSize = 13 };
    private readonly Button _previous = new() { Text = "‹ Previous" };
    private readonly Button _next = new() { Text = "Next ›" };
    private readonly Button _today = new() { Text = "This week" };
    private readonly Button _shopping = new() { Text = "Send ingredients to shopping" };
    private DateTime _weekStart = MealMonday(DateTime.UtcNow);
    private bool _loading;

    public MealsPage(MealsApiClient api)
    {
        _api = api;
        Title = "Meals";
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _previous.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _next.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _today.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _shopping.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _previous.Clicked += async (_, _) => { _weekStart = _weekStart.AddDays(-7); await LoadAsync(); };
        _next.Clicked += async (_, _) => { _weekStart = _weekStart.AddDays(7); await LoadAsync(); };
        _today.Clicked += async (_, _) => { _weekStart = MealMonday(DateTime.UtcNow); await LoadAsync(); };
        _shopping.Clicked += async (_, _) => await AddWeekToShoppingAsync();

        var heading = new Label { Text = "Meal plan", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var range = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
            Children = { _previous, _next }
        };
        var weekLabel = new Label { Text = WeekLabel(), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        weekLabel.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        range.Add(weekLabel, 1, 0);
        _previous.Clicked += (_, _) => weekLabel.Text = WeekLabel();
        _next.Clicked += (_, _) => weekLabel.Text = WeekLabel();
        _today.Clicked += (_, _) => weekLabel.Text = WeekLabel();

        var recipeHeading = new Label { Text = "Discover meals", FontSize = 22, FontAttributes = FontAttributes.Bold };
        recipeHeading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var newRecipe = new Button { Text = "＋ Create a recipe" };
        newRecipe.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        newRecipe.Clicked += async (_, _) => await CreateRecipeAsync();
        _recipeSearch.TextChanged += (_, _) => RenderRecipes();
        foreach (var filter in new[] { "All", "Dinner", "Breakfast", "Quick", "Vegetarian", "Kid-Friendly" })
        {
            var chip = new Button { Text = filter, FontSize = 12, Padding = new Thickness(14, 7), CornerRadius = 18 };
            chip.SetDynamicResource(VisualElement.StyleProperty, filter == "All" ? "FoPrimaryButtonStyle" : "FoSecondaryButtonStyle");
            chip.Clicked += (_, _) =>
            {
                _activeRecipeFilter = filter;
                foreach (var child in _recipeFilters.Children.OfType<Button>())
                    child.SetDynamicResource(VisualElement.StyleProperty,
                        child.Text == filter ? "FoPrimaryButtonStyle" : "FoSecondaryButtonStyle");
                RenderRecipes();
            };
            _recipeFilters.Children.Add(chip);
        }

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 12),
            Spacing = 14,
            Children =
            {
                heading, range, _today, _shopping, _status, _planItems,
                new BoxView { HeightRequest = 1, Opacity = 0.25 },
                recipeHeading, _recipeSearch, new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _recipeFilters, HeightRequest = 48 }, newRecipe, _recipeItems
            }
        };
        Content = new RefreshView
        {
            Content = new ScrollView { Content = content },
            Command = new Command(async () => await LoadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_planItems.Children.Count == 0 && !_loading)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        SetBusy(true);
        _status.Text = "Loading this week's meals…";
        try
        {
            var plan = await _api.GetWeekAsync(_weekStart);
            var recipes = await _api.GetRecipesAsync();
            _planItems.Children.Clear();
            _recipeItems.Children.Clear();
            foreach (var dayOffset in Enumerable.Range(0, 7))
            {
                var day = _weekStart.AddDays(dayOffset);
                var stack = new VerticalStackLayout { Spacing = 7 };
                var dayTitle = new Label { Text = day.ToString("dddd, MMMM d"), FontSize = 16, FontAttributes = FontAttributes.Bold };
                dayTitle.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
                stack.Children.Add(dayTitle);
                foreach (var slot in Slots)
                {
                    var entry = plan.Entries.FirstOrDefault(x => x.DayUtc.Date == day.Date && string.Equals(x.Slot, slot, StringComparison.OrdinalIgnoreCase));
                    var button = new Button
                    {
                        Text = $"{slot}: {(entry is null ? "＋ Add meal" : entry.DishName)}",
                        HorizontalOptions = LayoutOptions.Fill,
                        FontSize = 13
                    };
                    button.SetDynamicResource(VisualElement.StyleProperty, entry is null ? "FoGhostButtonStyle" : "FoSecondaryButtonStyle");
                    button.Clicked += async (_, _) => await EditMealAsync(day, slot, entry);
                    stack.Children.Add(button);
                }
                var card = new Border { Padding = 14, Content = stack };
                card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
                _planItems.Children.Add(card);
            }

            _allRecipes = recipes;
            RenderRecipes();

            _status.Text = $"{plan.Entries.Count} planned meal{(plan.Entries.Count == 1 ? "" : "s")} · {recipes.Count} recipes to discover";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "The request timed out. Pull to refresh and try again.";
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            _status.Text = "FamilyOS could not be reached. Check your connection and try again.";
        }
        catch (Exception)
        {
            _status.Text = "Something went wrong while loading meals.";
        }
        finally
        {
            _loading = false;
            SetBusy(false);
        }
    }

    private async Task EditMealAsync(DateTime day, string slot, MealEntryDto? existing)
    {
        if (existing is not null)
        {
            var action = await DisplayActionSheetAsync($"{day:dddd} · {slot}", "Cancel", "Remove meal", "Edit meal");
            if (action == "Remove meal")
            {
                if (!await DisplayAlertAsync("Remove meal", $"Remove {existing.DishName}?", "Remove", "Keep")) return;
                try
                {
                    await _api.RemoveEntryAsync(existing.Id);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Meal not removed", ex is FamilyApiException ? ex.Message : "Could not remove this meal.", "OK");
                }
                return;
            }
            if (action != "Edit meal") return;
        }

        var dish = await DisplayPromptAsync(existing is null ? "Add meal" : "Edit meal",
            $"{day:dddd} · {slot}", initialValue: existing?.DishName ?? "", maxLength: 160);
        if (string.IsNullOrWhiteSpace(dish)) return;
        var ingredients = await DisplayPromptAsync("Ingredients", "Optional, comma-separated ingredients",
            initialValue: existing?.IngredientsCsv ?? "", maxLength: 1000);
        try
        {
            await _api.UpsertEntryAsync(new MealEntryWriteDto(
                DateTime.SpecifyKind(day.Date, DateTimeKind.Utc), slot, dish.Trim(),
                existing?.Notes, string.IsNullOrWhiteSpace(ingredients) ? null : ingredients.Trim(), _weekStart));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Meal not saved", ex is FamilyApiException ? ex.Message : "Could not save this meal.", "OK");
        }
    }

    private async Task CreateRecipeAsync()
    {
        await Navigation.PushModalAsync(new NavigationPage(new RecipeEditorPage(_api)));
        await LoadAsync();
    }

    private void AddRecipeCard(RecipeDto recipe)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        var title = new Label { Text = recipe.Name, FontSize = 16, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(title);
        var detail = new Label
        {
            Text = $"{recipe.Servings} servings" + (string.IsNullOrWhiteSpace(recipe.IngredientsCsv) ? "" : $" · {recipe.IngredientsCsv}"),
            FontSize = 13
        };
        detail.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        stack.Children.Add(detail);
        if (!string.IsNullOrWhiteSpace(recipe.Instructions))
        {
            var instructions = new Label { Text = recipe.Instructions, FontSize = 13 };
            instructions.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
            stack.Children.Add(instructions);
        }

        var actions = new HorizontalStackLayout { Spacing = 8 };
        var apply = new Button { Text = "Add to plan", FontSize = 12 };
        apply.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        apply.Clicked += async (_, _) => await ApplyRecipeAsync(recipe);
        var shop = new Button { Text = "Shopping list", FontSize = 12 };
        shop.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        shop.Clicked += async (_, _) => await AddRecipeToShoppingAsync(recipe);
        actions.Children.Add(apply);
        actions.Children.Add(shop);
        if (!recipe.IsBuiltIn)
        {
            var delete = new Button { Text = "Delete", FontSize = 12 };
            delete.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            delete.Clicked += async (_, _) => await DeleteRecipeAsync(recipe);
            actions.Children.Add(delete);
        }
        stack.Children.Add(actions);
        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _recipeItems.Children.Add(card);
    }

    private void RenderRecipes()
    {
        _recipeItems.Children.Clear();
        var term = _recipeSearch.Text?.Trim() ?? string.Empty;
        var results = _allRecipes.Where(recipe =>
        {
            var textMatch = string.IsNullOrWhiteSpace(term)
                || recipe.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (recipe.IngredientsCsv?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (recipe.Tags?.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? false);
            var tagMatch = _activeRecipeFilter == "All"
                || (recipe.Tags?.Any(tag => string.Equals(tag, _activeRecipeFilter, StringComparison.OrdinalIgnoreCase)) ?? false);
            return textMatch && tagMatch;
        }).ToList();

        if (results.Count == 0)
        {
            AddInfo(_recipeItems, "No matching meals", "Try another search or switch the category filter.");
            return;
        }

        foreach (var recipe in results)
            AddRecipeCard(recipe);
    }

    private async Task ApplyRecipeAsync(RecipeDto recipe)
    {
        var days = Enumerable.Range(0, 7).Select(i => _weekStart.AddDays(i)).ToArray();
        var dayChoices = days.Select(d => d.ToString("dddd, MMM d")).ToArray();
        var selectedDay = await DisplayActionSheetAsync($"Choose a day for {recipe.Name}", "Cancel", null, dayChoices);
        var dayIndex = Array.IndexOf(dayChoices, selectedDay);
        if (dayIndex < 0) return;
        var slot = await DisplayActionSheetAsync("Choose a meal", "Cancel", null, Slots);
        if (string.IsNullOrWhiteSpace(slot) || slot == "Cancel") return;
        try
        {
            await _api.ApplyRecipeAsync(recipe.Id, new ApplyRecipeDto(days[dayIndex], slot, _weekStart));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Recipe not applied", ex is FamilyApiException ? ex.Message : "Could not add the recipe.", "OK");
        }
    }

    private async Task AddRecipeToShoppingAsync(RecipeDto recipe)
    {
        var targetText = await DisplayPromptAsync("Shopping list", "Target servings (leave blank for recipe default)",
            initialValue: recipe.Servings.ToString(), maxLength: 3, keyboard: Keyboard.Numeric);
        if (targetText is null) return;
        int? target = int.TryParse(targetText, out var parsed) && parsed > 0 ? parsed : null;
        try
        {
            var result = await _api.AddRecipeToShoppingAsync(recipe.Id, target);
            await DisplayAlertAsync("Shopping list", $"Added {result.Added} new item(s).", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Shopping list not updated", ex is FamilyApiException ? ex.Message : "Could not add ingredients.", "OK");
        }
    }

    private async Task DeleteRecipeAsync(RecipeDto recipe)
    {
        if (!await DisplayAlertAsync("Delete recipe", $"Delete “{recipe.Name}”?", "Delete", "Keep")) return;
        try
        {
            await _api.DeleteRecipeAsync(recipe.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Recipe not deleted", ex is FamilyApiException ? ex.Message : "Could not delete this recipe.", "OK");
        }
    }

    private async Task AddWeekToShoppingAsync()
    {
        try
        {
            var result = await _api.AddWeekToShoppingAsync(_weekStart);
            await DisplayAlertAsync("Shopping list", $"Added {result.Added} new ingredient item(s).", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Shopping list not updated", ex is FamilyApiException ? ex.Message : "Could not add meal ingredients.", "OK");
        }
    }

    private static DateTime MealMonday(DateTime value)
    {
        var day = DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
        var offset = day.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)day.DayOfWeek - 1;
        return day.AddDays(-offset);
    }

    private string WeekLabel() => $"{_weekStart:MMM d} – {_weekStart.AddDays(6):MMM d, yyyy}";

    private void SetBusy(bool busy)
    {
        _previous.IsEnabled = !busy;
        _next.IsEnabled = !busy;
        _today.IsEnabled = !busy;
        _shopping.IsEnabled = !busy;
    }

    private static void AddInfo(VerticalStackLayout target, string title, string detail)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        var heading = new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var body = new Label { Text = detail, FontSize = 13 };
        body.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        stack.Children.Add(heading);
        stack.Children.Add(body);
        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        target.Children.Add(card);
    }
}
