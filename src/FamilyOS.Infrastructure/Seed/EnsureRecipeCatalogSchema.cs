using System.Globalization;
using System.Text.RegularExpressions;
using FamilyOS.Domain.Meals;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

/// <summary>Creates normalized recipe catalog tables and idempotently seeds shared starter content.</summary>
public static class EnsureRecipeCatalogSchema
{
    private static readonly (string Name, string Category, string? Aliases)[] Ingredients =
    [
        ("All-purpose flour", "Pantry", "plain flour"), ("Bread flour", "Pantry", "strong flour"),
        ("Granulated sugar", "Pantry", "white sugar"), ("Brown sugar", "Pantry", "light brown sugar;dark brown sugar"),
        ("Powdered sugar", "Pantry", "icing sugar;confectioners sugar"), ("Baking powder", "Pantry", null),
        ("Baking soda", "Pantry", "bicarbonate of soda"), ("Salt", "Spices", "table salt"),
        ("Black pepper", "Spices", "ground black pepper"), ("Olive oil", "Pantry", "extra virgin olive oil"),
        ("Vegetable oil", "Pantry", "canola oil"), ("Butter", "Dairy", "unsalted butter;salted butter"),
        ("Eggs", "Dairy", "large eggs"), ("Milk", "Dairy", "whole milk"), ("Heavy cream", "Dairy", "whipping cream"),
        ("Sour cream", "Dairy", null), ("Cream cheese", "Dairy", null),
        ("Shredded cheddar cheese", "Dairy", "cheddar"), ("Mozzarella cheese", "Dairy", "mozzarella"),
        ("Parmesan cheese", "Dairy", "parmigiano"), ("Plain Greek yogurt", "Dairy", "greek yogurt"),
        ("Chicken breast", "Meat", "boneless skinless chicken breast"), ("Chicken thighs", "Meat", "boneless chicken thighs"),
        ("Ground beef", "Meat", "minced beef"), ("Steak", "Meat", "beef steak"), ("Bacon", "Meat", null),
        ("Italian sausage", "Meat", "sausage"), ("Ground turkey", "Meat", "minced turkey"),
        ("Salmon fillet", "Seafood", "salmon"), ("Shrimp", "Seafood", "prawns"),
        ("Yellow onion", "Produce", "onion"), ("Red onion", "Produce", null), ("Garlic", "Produce", "garlic cloves"),
        ("Russet potatoes", "Produce", "potatoes"), ("Sweet potatoes", "Produce", null),
        ("Carrots", "Produce", null), ("Celery", "Produce", null), ("Broccoli", "Produce", null),
        ("Bell pepper", "Produce", "capsicum;bell peppers"), ("Tomatoes", "Produce", "fresh tomato;tomato"),
        ("Cherry tomatoes", "Produce", null), ("Cucumber", "Produce", null), ("Spinach", "Produce", "baby spinach"),
        ("Lettuce", "Produce", "romaine lettuce"), ("Avocado", "Produce", null), ("Lemon", "Produce", null),
        ("Lime", "Produce", null), ("Fresh parsley", "Produce", "parsley"), ("Fresh cilantro", "Produce", "coriander leaves"),
        ("Fresh basil", "Produce", "basil"), ("Apples", "Produce", null), ("Bananas", "Produce", null),
        ("Blueberries", "Produce", null), ("Strawberries", "Produce", null),
        ("Linguine", "Pantry", null), ("Spaghetti", "Pantry", null), ("Penne pasta", "Pantry", "penne"),
        ("Macaroni", "Pantry", "elbow macaroni"), ("White rice", "Pantry", "long grain rice;rice"),
        ("Brown rice", "Pantry", null), ("Quinoa", "Pantry", null), ("Rolled oats", "Pantry", "old fashioned oats"),
        ("Breadcrumbs", "Pantry", "bread crumbs"), ("Panko breadcrumbs", "Pantry", "panko"),
        ("Tortillas", "Bakery", "flour tortillas"), ("Sandwich bread", "Bakery", "bread"),
        ("Hamburger buns", "Bakery", "burger buns"), ("Pita bread", "Bakery", "pita"),
        ("Canned diced tomatoes", "Canned", "tinned chopped tomatoes"), ("Tomato paste", "Canned", null),
        ("Marinara sauce", "Canned", "pasta sauce"), ("Chicken broth", "Canned", "chicken stock"),
        ("Beef broth", "Canned", "beef stock"), ("Black beans", "Canned", null), ("Kidney beans", "Canned", null),
        ("Chickpeas", "Canned", "garbanzo beans"), ("Corn", "Canned", "sweetcorn"),
        ("Peanut butter", "Pantry", null), ("Honey", "Pantry", null), ("Maple syrup", "Pantry", null),
        ("Soy sauce", "Condiments", null), ("Worcestershire sauce", "Condiments", null),
        ("Dijon mustard", "Condiments", "mustard"), ("Mayonnaise", "Condiments", "mayo"),
        ("Ketchup", "Condiments", null), ("BBQ sauce", "Condiments", "barbecue sauce"),
        ("Hot sauce", "Condiments", null), ("Italian seasoning", "Spices", null), ("Paprika", "Spices", "smoked paprika"),
        ("Chili powder", "Spices", null), ("Ground cumin", "Spices", "cumin"),
        ("Dried oregano", "Spices", "oregano"), ("Dried thyme", "Spices", "thyme"),
        ("Cinnamon", "Spices", "ground cinnamon"), ("Red pepper flakes", "Spices", "crushed red pepper"),
        ("Vanilla extract", "Baking", "vanilla"), ("Cocoa powder", "Baking", null),
        ("Chocolate chips", "Baking", null), ("Cornstarch", "Baking", "corn flour"),
        ("Active dry yeast", "Baking", "yeast"), ("Frozen peas", "Frozen", null),
        ("Frozen mixed vegetables", "Frozen", "mixed veg"), ("Frozen berries", "Frozen", null),
        ("Orange juice", "Beverages", null), ("Chicken stock", "Canned", "stock")
    ];

    private static readonly string[] Tags =
    [
        "Breakfast", "Lunch", "Dinner", "Snack", "Dessert", "Vegetarian", "Vegan", "Gluten-Free",
        "Dairy-Free", "High-Protein", "Low-Carb", "Budget-Friendly", "Kid-Friendly", "Quick",
        "Under-30-Minutes", "One-Pot", "Slow-Cooker", "Air-Fryer", "Meal-Prep", "Freezer-Friendly",
        "Comfort-Food", "Italian", "Mexican", "American", "Asian-Inspired", "Mediterranean",
        "Healthy", "Family-Favorite"
    ];

    private static readonly BuiltInRecipe[] Recipes =
    [
        new("Chicken fajita bowls", 4, "1 lb chicken breast, 2 bell peppers, 1 onion, 2 cups rice, 1 tbsp olive oil, 2 tsp chili powder, 1 tsp cumin, lime",
            "Cook rice. Season and sauté sliced chicken in oil. Add peppers and onion; cook until tender. Serve over rice with lime.",
            ["Dinner", "Mexican", "High-Protein", "Family-Favorite"]),
        new("Creamy garlic chicken pasta", 4, "1 lb chicken breast, 12 oz penne pasta, 3 cloves garlic, 1 cup heavy cream, 1/2 cup parmesan cheese, 1 tbsp olive oil",
            "Cook pasta. Sauté chicken until cooked through; add garlic and cream. Simmer, stir in parmesan, and toss with pasta.",
            ["Dinner", "Italian", "Comfort-Food", "Family-Favorite"]),
        new("Beef tacos", 4, "1 lb ground beef, 8 tortillas, 1 packet taco seasoning, 1 cup shredded cheddar cheese, 1 tomato, 1 cup lettuce",
            "Brown beef, drain, add seasoning and water per packet. Spoon into warm tortillas and add toppings.",
            ["Dinner", "Mexican", "Budget-Friendly", "Kid-Friendly"]),
        new("Sheet-pan lemon chicken", 4, "1.5 lb chicken thighs, 1 lb potatoes, 3 carrots, 2 tbsp olive oil, 1 lemon, 1 tsp dried thyme",
            "Heat oven to 425 F. Toss chopped vegetables with oil and thyme. Add chicken and lemon; roast 30-40 minutes until chicken reaches 165 F.",
            ["Dinner", "One-Pot", "Meal-Prep", "Gluten-Free"]),
        new("Slow cooker pulled pork", 8, "3 lb pork shoulder, 1 cup BBQ sauce, 1 onion, 1/2 cup chicken broth, 8 hamburger buns",
            "Add pork, sliced onion, broth, and half the BBQ sauce to slow cooker. Cook low 8 hours, shred, and stir in remaining sauce.",
            ["Dinner", "Slow-Cooker", "Budget-Friendly", "Freezer-Friendly"]),
        new("Classic spaghetti and meat sauce", 4, "1 lb ground beef, 12 oz spaghetti, 24 oz marinara sauce, 1 onion, 2 cloves garlic, 1/4 cup parmesan cheese",
            "Cook spaghetti. Brown beef with onion and garlic, drain, add marinara and simmer 15 minutes. Serve over pasta with parmesan.",
            ["Dinner", "Italian", "Budget-Friendly", "Family-Favorite"]),
        new("One-pot cheesy beef macaroni", 6, "1 lb ground beef, 2 cups macaroni, 2 cups beef broth, 1 cup milk, 1.5 cups shredded cheddar cheese, 1 tsp paprika",
            "Brown beef in a pot. Add macaroni and broth; simmer until pasta is tender. Stir in milk and cheese until creamy.",
            ["Dinner", "One-Pot", "Kid-Friendly", "Comfort-Food"]),
        new("Baked salmon with rice", 4, "4 salmon fillets, 1 cup white rice, 1 lemon, 1 tbsp olive oil, 1 tsp garlic, 1 tsp black pepper",
            "Cook rice. Brush salmon with oil, garlic, pepper, and lemon. Bake at 400 F for 12-15 minutes or until flaky.",
            ["Dinner", "High-Protein", "Gluten-Free", "Quick"]),
        new("Chicken quesadillas", 4, "2 cups cooked chicken, 4 tortillas, 1.5 cups shredded cheddar cheese, 1 bell pepper, 1 tbsp olive oil",
            "Fill tortillas with chicken, cheese, and sliced pepper. Fold and cook in oiled skillet until crisp and cheese melts.",
            ["Lunch", "Dinner", "Mexican", "Quick", "Kid-Friendly"]),
        new("Turkey meatballs", 4, "1 lb ground turkey, 1 egg, 1/2 cup breadcrumbs, 1/4 cup parmesan cheese, 2 cups marinara sauce",
            "Mix turkey, egg, breadcrumbs, and parmesan. Form meatballs and bake at 400 F for 18-22 minutes. Warm in marinara.",
            ["Dinner", "High-Protein", "Meal-Prep", "Italian"]),
        new("Vegetable fried rice", 4, "3 cups cooked rice, 2 eggs, 1 cup frozen mixed vegetables, 2 tbsp soy sauce, 1 tbsp vegetable oil, 2 green onions",
            "Scramble eggs and set aside. Stir-fry vegetables, add rice and soy sauce, then fold in eggs and green onions.",
            ["Lunch", "Dinner", "Vegetarian", "Budget-Friendly", "Quick"]),
        new("Creamy tomato soup", 4, "28 oz canned diced tomatoes, 1 onion, 2 cloves garlic, 1 cup vegetable broth, 1/2 cup heavy cream, 1 tbsp olive oil",
            "Sauté onion and garlic. Add tomatoes and broth; simmer 20 minutes. Blend smooth and stir in cream.",
            ["Lunch", "Vegetarian", "Comfort-Food", "Budget-Friendly"]),
        new("BBQ chicken sandwiches", 6, "2 lb chicken breast, 1 cup BBQ sauce, 6 hamburger buns, 1/2 onion, 1/2 cup chicken broth",
            "Slow cook chicken with broth and half the BBQ sauce until shreddable. Shred, stir in remaining sauce, and serve on buns.",
            ["Dinner", "Slow-Cooker", "Kid-Friendly", "Meal-Prep"]),
        new("Breakfast egg muffins", 6, "8 eggs, 1/2 cup milk, 1 cup shredded cheddar cheese, 1 bell pepper, 1/2 cup spinach",
            "Whisk eggs and milk. Stir in diced pepper, spinach, and cheese. Pour into greased muffin tin and bake at 350 F for 18-22 minutes.",
            ["Breakfast", "High-Protein", "Meal-Prep", "Gluten-Free"]),
        new("Blueberry oatmeal", 4, "2 cups rolled oats, 3 cups milk, 1 cup blueberries, 1 tbsp honey, 1 tsp cinnamon",
            "Simmer oats and milk until creamy. Stir in cinnamon and honey; top with blueberries.",
            ["Breakfast", "Vegetarian", "Budget-Friendly", "Kid-Friendly"]),
        new("Banana pancakes", 4, "1.5 cups all-purpose flour, 2 bananas, 1 egg, 1 cup milk, 2 tsp baking powder, 2 tbsp butter",
            "Mash bananas. Whisk with egg and milk, then fold in flour and baking powder. Cook pancakes in buttered skillet until golden.",
            ["Breakfast", "Kid-Friendly", "Budget-Friendly"]),
        new("Greek chicken wraps", 4, "1 lb chicken breast, 4 pita bread, 1 cucumber, 1 tomato, 1 cup plain Greek yogurt, 1 lemon, 1 tsp garlic",
            "Season and grill chicken. Mix yogurt, lemon, and garlic for sauce. Fill pita with chicken, cucumber, tomato, and sauce.",
            ["Lunch", "Dinner", "Mediterranean", "High-Protein"]),
        new("Black bean quesadillas", 4, "1 can black beans, 4 tortillas, 1 cup shredded cheddar cheese, 1/2 bell pepper, 1 tsp cumin",
            "Mash half the beans. Spread on tortillas, add cheese and pepper, fold, and toast in skillet until crisp.",
            ["Lunch", "Vegetarian", "Budget-Friendly", "Quick"]),
        new("Honey garlic shrimp bowls", 4, "1 lb shrimp, 2 cups white rice, 3 cloves garlic, 2 tbsp honey, 2 tbsp soy sauce, 1 tbsp olive oil",
            "Cook rice. Sauté shrimp in oil; add garlic, honey, and soy sauce. Simmer until shrimp are opaque and sauce thickens.",
            ["Dinner", "Asian-Inspired", "Quick", "High-Protein"]),
        new("Loaded baked potatoes", 4, "4 russet potatoes, 1 cup sour cream, 1 cup shredded cheddar cheese, 4 slices bacon, 2 green onions",
            "Bake potatoes at 425 F for 45-60 minutes until tender. Split and top with sour cream, cheese, bacon, and onions.",
            ["Dinner", "Budget-Friendly", "Gluten-Free", "Comfort-Food"]),
        new("Chicken noodle soup", 6, "1 lb chicken breast, 8 oz linguine, 3 carrots, 3 celery stalks, 1 onion, 8 cups chicken broth, 1 tsp dried thyme",
            "Simmer chicken and vegetables in broth until chicken is cooked. Remove and shred chicken. Add pasta and cook tender; return chicken.",
            ["Lunch", "Dinner", "Comfort-Food", "Freezer-Friendly"]),
        new("Beef and broccoli stir-fry", 4, "1 lb steak, 4 cups broccoli, 2 tbsp soy sauce, 2 cloves garlic, 1 tbsp honey, 2 cups white rice",
            "Cook rice. Sear thinly sliced steak, add broccoli with a splash of water, cover until tender. Stir in soy, garlic, and honey.",
            ["Dinner", "Asian-Inspired", "High-Protein", "Quick"]),
        new("Spinach and cheese omelet", 1, "2 eggs, 1/4 cup shredded cheddar cheese, 1/2 cup spinach, 1 tsp butter",
            "Whisk eggs. Melt butter in skillet, add eggs, then spinach and cheese. Fold when set.",
            ["Breakfast", "Vegetarian", "Quick", "Gluten-Free"]),
        new("Baked ziti", 8, "1 lb penne pasta, 24 oz marinara sauce, 2 cups mozzarella cheese, 1/2 cup parmesan cheese, 1 cup ricotta cheese",
            "Cook pasta until just shy of tender. Mix with marinara and ricotta, top with mozzarella and parmesan. Bake at 375 F for 25 minutes.",
            ["Dinner", "Italian", "Comfort-Food", "Freezer-Friendly"]),
        new("Crispy chicken tenders", 4, "1.5 lb chicken breast, 1 cup breadcrumbs, 1 egg, 1/2 cup all-purpose flour, 1 tsp paprika",
            "Cut chicken into strips. Coat in flour, beaten egg, then breadcrumbs with paprika. Bake at 425 F for 18-22 minutes, turning once.",
            ["Dinner", "Kid-Friendly", "Air-Fryer", "High-Protein"]),
        new("Tuna pasta salad", 4, "12 oz penne pasta, 2 cans tuna, 1/2 cup mayonnaise, 2 celery stalks, 1/2 red onion, 1 tbsp lemon juice",
            "Cook pasta and cool. Fold in drained tuna, mayonnaise, diced celery and onion, and lemon juice. Chill before serving.",
            ["Lunch", "Budget-Friendly", "Meal-Prep"]),
        new("Lentil vegetable curry", 6, "1.5 cups lentils, 1 onion, 2 carrots, 1 can diced tomatoes, 2 tbsp curry powder, 4 cups vegetable broth",
            "Sauté onion and carrots. Add lentils, tomatoes, curry powder, and broth. Simmer 30-35 minutes until lentils are tender.",
            ["Dinner", "Vegetarian", "Vegan", "Budget-Friendly", "One-Pot"]),
        new("Caprese grilled cheese", 2, "4 slices sandwich bread, 4 slices mozzarella cheese, 1 tomato, 4 basil leaves, 2 tbsp butter",
            "Layer mozzarella, tomato, and basil between bread. Butter outside and cook in skillet until golden and cheese melts.",
            ["Lunch", "Vegetarian", "Quick", "Kid-Friendly"]),
        new("Turkey chili", 6, "1 lb ground turkey, 1 can kidney beans, 1 can black beans, 1 can diced tomatoes, 1 onion, 2 tbsp chili powder",
            "Brown turkey and onion. Add beans, tomatoes, chili powder, and 1 cup water. Simmer 25-30 minutes.",
            ["Dinner", "One-Pot", "High-Protein", "Freezer-Friendly"]),
        new("Apple cinnamon overnight oats", 2, "1 cup rolled oats, 1 cup milk, 1 apple, 1/2 cup plain Greek yogurt, 1 tsp cinnamon, 1 tbsp honey",
            "Combine oats, milk, yogurt, diced apple, cinnamon, and honey. Refrigerate overnight and stir before serving.",
            ["Breakfast", "Vegetarian", "Meal-Prep", "Quick"])
    ];

    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS familyos."IngredientCatalog" (
                "Id" uuid NOT NULL PRIMARY KEY, "FamilyId" uuid NULL,
                "Name" character varying(160) NOT NULL, "SearchKey" character varying(180) NOT NULL,
                "Category" character varying(60) NOT NULL, "AliasesCsv" character varying(800) NULL,
                "IsBuiltIn" boolean NOT NULL DEFAULT false,
                "CreatedAtUtc" timestamp with time zone NOT NULL, "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS "IX_IngredientCatalog_SearchKey" ON familyos."IngredientCatalog" ("SearchKey");
            CREATE INDEX IF NOT EXISTS "IX_IngredientCatalog_FamilyId_Name" ON familyos."IngredientCatalog" ("FamilyId", "Name");

            CREATE TABLE IF NOT EXISTS familyos."RecipeIngredients" (
                "Id" uuid NOT NULL PRIMARY KEY, "RecipeId" uuid NOT NULL, "IngredientId" uuid NOT NULL,
                "Quantity" numeric(12,3) NULL, "Unit" character varying(40) NULL,
                "PreparationNote" character varying(240) NULL, "Section" character varying(100) NULL,
                "SortOrder" integer NOT NULL, "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL, "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS "IX_RecipeIngredients_RecipeId_SortOrder" ON familyos."RecipeIngredients" ("RecipeId", "SortOrder");
            CREATE INDEX IF NOT EXISTS "IX_RecipeIngredients_RecipeId_IngredientId" ON familyos."RecipeIngredients" ("RecipeId", "IngredientId");

            CREATE TABLE IF NOT EXISTS familyos."RecipeTags" (
                "Id" uuid NOT NULL PRIMARY KEY, "FamilyId" uuid NULL, "Name" character varying(60) NOT NULL,
                "Slug" character varying(80) NOT NULL, "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL, "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS "IX_RecipeTags_Slug" ON familyos."RecipeTags" ("Slug");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeTags_FamilyId_Slug" ON familyos."RecipeTags" ("FamilyId", "Slug");

            CREATE TABLE IF NOT EXISTS familyos."RecipeTagLinks" (
                "Id" uuid NOT NULL PRIMARY KEY, "RecipeId" uuid NOT NULL, "TagId" uuid NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL, "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeTagLinks_RecipeId_TagId" ON familyos."RecipeTagLinks" ("RecipeId", "TagId");

            CREATE TABLE IF NOT EXISTS familyos."RecipeInstructionSteps" (
                "Id" uuid NOT NULL PRIMARY KEY, "RecipeId" uuid NOT NULL, "StepNumber" integer NOT NULL,
                "Text" character varying(2000) NOT NULL, "DurationMinutes" integer NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL, "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeInstructionSteps_RecipeId_StepNumber"
                ON familyos."RecipeInstructionSteps" ("RecipeId", "StepNumber");
            """);

        await SeedIngredientsAsync(db);
        await SeedTagsAsync(db);
        await SeedRecipesAsync(db, logger);
    }

    private static async Task SeedIngredientsAsync(FamilyOsDbContext db)
    {
        var existing = await db.IngredientCatalog.Where(x => x.FamilyId == null).ToListAsync();
        var keys = existing.Select(x => x.SearchKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Ingredients)
        {
            var key = Normalize(item.Name);
            if (keys.Add(key))
                db.IngredientCatalog.Add(IngredientCatalogItem.CreateBuiltIn(item.Name, item.Category, item.Aliases));
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedTagsAsync(FamilyOsDbContext db)
    {
        var existing = await db.RecipeTags.Where(x => x.FamilyId == null).ToListAsync();
        var slugs = existing.Select(x => x.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Tags)
        {
            var slug = Slug(name);
            if (slugs.Add(slug))
                db.RecipeTags.Add(RecipeTag.CreateBuiltIn(name));
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedRecipesAsync(FamilyOsDbContext db, ILogger logger)
    {
        var existing = await db.Recipes.Where(x => x.FamilyId == Guid.Empty).ToListAsync();
        var byName = existing.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var seed in Recipes)
        {
            if (byName.ContainsKey(seed.Name)) continue;
            var recipe = Recipe.Create(Guid.Empty, seed.Name, seed.Servings, seed.Ingredients, seed.Instructions);
            db.Recipes.Add(recipe);
            byName.Add(seed.Name, recipe);
        }
        await db.SaveChangesAsync();

        var recipeIds = byName.Values.Select(x => x.Id).ToArray();
        var tags = await db.RecipeTags.Where(x => x.FamilyId == null).ToListAsync();
        var tagBySlug = tags.ToDictionary(x => x.Slug, StringComparer.OrdinalIgnoreCase);
        var links = await db.RecipeTagLinks.Where(x => recipeIds.Contains(x.RecipeId)).ToListAsync();
        var linkKeys = links.Select(x => (x.RecipeId, x.TagId)).ToHashSet();
        foreach (var seed in Recipes)
        {
            var recipe = byName[seed.Name];
            foreach (var tagName in seed.Tags)
            {
                if (!tagBySlug.TryGetValue(Slug(tagName), out var tag)) continue;
                if (linkKeys.Add((recipe.Id, tag.Id)))
                    db.RecipeTagLinks.Add(RecipeTagLink.Create(recipe.Id, tag.Id));
            }
        }

        var structured = await db.RecipeIngredients.Where(x => recipeIds.Contains(x.RecipeId))
            .Select(x => x.RecipeId).Distinct().ToListAsync();
        var structuredIds = structured.ToHashSet();
        var catalog = await db.IngredientCatalog.Where(x => x.FamilyId == null).ToListAsync();
        var catalogByKey = new Dictionary<string, IngredientCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in catalog)
        {
            catalogByKey[item.SearchKey] = item;
            foreach (var alias in (item.AliasesCsv ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                catalogByKey.TryAdd(Normalize(alias), item);
        }

        foreach (var seed in Recipes)
        {
            var recipe = byName[seed.Name];
            if (!structuredIds.Add(recipe.Id)) continue;
            var lines = seed.Ingredients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var index = 0; index < lines.Length; index++)
            {
                var (quantity, unit, ingredientName) = ParseIngredient(lines[index]);
                var key = Normalize(ingredientName);
                if (!catalogByKey.TryGetValue(key, out var ingredient))
                {
                    ingredient = IngredientCatalogItem.CreateBuiltIn(ingredientName, "Other");
                    db.IngredientCatalog.Add(ingredient);
                    catalogByKey[key] = ingredient;
                }
                db.RecipeIngredients.Add(RecipeIngredient.Create(recipe.Id, ingredient.Id,
                    quantity, unit, null, index));
            }

            var steps = seed.Instructions.Split(". ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var index = 0; index < steps.Length; index++)
                db.RecipeInstructionSteps.Add(RecipeInstructionStep.Create(recipe.Id, index + 1,
                    steps[index].TrimEnd('.')));
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Built-in recipe catalog ready: {RecipeCount} recipes, {IngredientCount} ingredient entries, {TagCount} discovery tags.",
            Recipes.Length, Ingredients.Length, Tags.Length);
    }

    private static (decimal? Quantity, string? Unit, string Name) ParseIngredient(string line)
    {
        var match = Regex.Match(line, @"^(?<quantity>\d+(?:\.\d+)?|\d+/\d+)\s+(?<unit>[A-Za-z]+)\s+(?<name>.+)$");
        if (!match.Success) return (null, null, line.Trim());
        decimal quantity;
        var quantityText = match.Groups["quantity"].Value;
        if (quantityText.Contains('/'))
        {
            var parts = quantityText.Split('/');
            quantity = decimal.Parse(parts[0], CultureInfo.InvariantCulture) /
                decimal.Parse(parts[1], CultureInfo.InvariantCulture);
        }
        else quantity = decimal.Parse(quantityText, CultureInfo.InvariantCulture);
        return (quantity, match.Groups["unit"].Value.ToLowerInvariant(), match.Groups["name"].Value.Trim());
    }

    private static string Normalize(string value)
        => string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Slug(string value)
        => string.Join('-', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private sealed record BuiltInRecipe(string Name, int Servings, string Ingredients, string Instructions, string[] Tags);
}
