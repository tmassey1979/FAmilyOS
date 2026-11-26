using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class RecipeCatalogEntityTests
{
    [Fact]
    public void Built_in_ingredient_matches_canonical_name_and_semicolon_aliases()
    {
        var ingredient = IngredientCatalogItem.CreateBuiltIn(
            "Bell pepper", "Produce", "capsicum;bell peppers");

        ingredient.Matches("bell pepper").Should().BeTrue();
        ingredient.Matches("capsicum").Should().BeTrue();
        ingredient.Matches("bell peppers").Should().BeTrue();
        ingredient.Matches("chicken").Should().BeFalse();
        ingredient.IsBuiltIn.Should().BeTrue();
        ingredient.FamilyId.Should().BeNull();
    }

    [Fact]
    public void Recipe_ingredient_preserves_measurement_and_preparation()
    {
        var row = RecipeIngredient.Create(Guid.NewGuid(), Guid.NewGuid(),
            1.5m, "cups", "finely diced", 2, "Sauce");

        row.Quantity.Should().Be(1.5m);
        row.Unit.Should().Be("cups");
        row.PreparationNote.Should().Be("finely diced");
        row.SortOrder.Should().Be(2);
        row.Section.Should().Be("Sauce");
    }

    [Fact]
    public void Recipe_ingredient_rejects_negative_quantity()
    {
        var act = () => RecipeIngredient.Create(Guid.NewGuid(), Guid.NewGuid(), -1m, "cup", null, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Recipe_tag_generates_searchable_slug()
    {
        var tag = RecipeTag.CreateBuiltIn("Kid-Friendly");
        tag.Name.Should().Be("Kid-Friendly");
        tag.Slug.Should().Be("kid-friendly");
        tag.FamilyId.Should().BeNull();
    }

    [Fact]
    public void Recipe_stores_import_attribution_and_cooking_metadata()
    {
        var recipe = Recipe.Create(Guid.NewGuid(), "Imported pasta", 4, null, "Boil pasta.",
            "A quick dinner", "https://example.com/pasta.jpg", "https://example.com/recipe",
            10, 20, "Italian", "Easy");

        recipe.Description.Should().Be("A quick dinner");
        recipe.ImageUrl.Should().Be("https://example.com/pasta.jpg");
        recipe.SourceUrl.Should().Be("https://example.com/recipe");
        recipe.PrepMinutes.Should().Be(10);
        recipe.CookMinutes.Should().Be(20);
        recipe.Cuisine.Should().Be("Italian");
        recipe.Difficulty.Should().Be("Easy");
    }
}
