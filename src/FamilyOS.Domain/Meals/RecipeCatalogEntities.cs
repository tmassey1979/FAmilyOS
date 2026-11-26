using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

/// <summary>Canonical ingredient used by built-in and family recipes.</summary>
public sealed class IngredientCatalogItem : Entity
{
    public Guid? FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    public string SearchKey { get; private set; } = "";
    public string Category { get; private set; } = "Pantry";
    public string? AliasesCsv { get; private set; }
    public bool IsBuiltIn { get; private set; }

    private IngredientCatalogItem() { }

    public static IngredientCatalogItem CreateBuiltIn(string name, string category, string? aliases = null)
        => Create(null, name, category, aliases, true);

    public static IngredientCatalogItem CreateFamily(Guid familyId, string name, string category, string? aliases = null)
        => Create(familyId, name, category, aliases, false);

    private static IngredientCatalogItem Create(Guid? familyId, string name, string category, string? aliases, bool builtIn)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Ingredient name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Ingredient category is required.", nameof(category));
        var normalized = name.Trim();
        return new IngredientCatalogItem
        {
            FamilyId = familyId,
            Name = normalized,
            SearchKey = Normalize(normalized),
            Category = category.Trim(),
            AliasesCsv = string.IsNullOrWhiteSpace(aliases) ? null : aliases.Trim(),
            IsBuiltIn = builtIn
        };
    }

    public bool Matches(string query)
    {
        var key = Normalize(query);
        return SearchKey.Contains(key, StringComparison.Ordinal)
            || (AliasesCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => Normalize(x).Contains(key, StringComparison.Ordinal)) ?? false);
    }

    private static string Normalize(string value)
        => string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>Structured quantity and unit for a recipe ingredient.</summary>
public sealed class RecipeIngredient : Entity
{
    public Guid RecipeId { get; private set; }
    public Guid IngredientId { get; private set; }
    public decimal? Quantity { get; private set; }
    public string? Unit { get; private set; }
    public string? PreparationNote { get; private set; }
    public string? Section { get; private set; }
    public int SortOrder { get; private set; }

    private RecipeIngredient() { }

    public static RecipeIngredient Create(Guid recipeId, Guid ingredientId, decimal? quantity, string? unit,
        string? preparationNote, int sortOrder, string? section = null)
    {
        if (quantity is < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        return new RecipeIngredient
        {
            RecipeId = recipeId,
            IngredientId = ingredientId,
            Quantity = quantity,
            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            PreparationNote = string.IsNullOrWhiteSpace(preparationNote) ? null : preparationNote.Trim(),
            Section = string.IsNullOrWhiteSpace(section) ? null : section.Trim(),
            SortOrder = sortOrder
        };
    }
}

/// <summary>Reusable discovery tag, globally seeded or family-created.</summary>
public sealed class RecipeTag : Entity
{
    public Guid? FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    private RecipeTag() { }

    public static RecipeTag CreateBuiltIn(string name) => Create(null, name);
    public static RecipeTag CreateFamily(Guid familyId, string name) => Create(familyId, name);
    private static RecipeTag Create(Guid? familyId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tag name is required.", nameof(name));
        var clean = name.Trim();
        return new RecipeTag { FamilyId = familyId, Name = clean,
            Slug = string.Join('-', clean.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)) };
    }
}

/// <summary>Many-to-many recipe/tag association.</summary>
public sealed class RecipeTagLink : Entity
{
    public Guid RecipeId { get; private set; }
    public Guid TagId { get; private set; }
    private RecipeTagLink() { }
    public static RecipeTagLink Create(Guid recipeId, Guid tagId)
        => new() { RecipeId = recipeId, TagId = tagId };
}

/// <summary>Ordered instruction step.</summary>
public sealed class RecipeInstructionStep : Entity
{
    public Guid RecipeId { get; private set; }
    public int StepNumber { get; private set; }
    public string Text { get; private set; } = "";
    public int? DurationMinutes { get; private set; }
    private RecipeInstructionStep() { }
    public static RecipeInstructionStep Create(Guid recipeId, int stepNumber, string text, int? durationMinutes = null)
    {
        if (stepNumber < 1) throw new ArgumentOutOfRangeException(nameof(stepNumber));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Step text is required.", nameof(text));
        if (durationMinutes is < 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        return new RecipeInstructionStep { RecipeId = recipeId, StepNumber = stepNumber,
            Text = text.Trim(), DurationMinutes = durationMinutes };
    }
}
