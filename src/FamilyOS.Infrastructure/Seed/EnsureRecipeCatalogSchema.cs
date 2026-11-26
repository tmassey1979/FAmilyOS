using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

/// <summary>Creates normalized recipe catalog tables and idempotently seeds common ingredients and discovery tags.</summary>
public static class EnsureRecipeCatalogSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS familyos."IngredientCatalog" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "FamilyId" uuid NULL,
                "Name" character varying(160) NOT NULL,
                "SearchKey" character varying(180) NOT NULL,
                "Category" character varying(60) NOT NULL,
                "AliasesCsv" character varying(800) NULL,
                "IsBuiltIn" boolean NOT NULL DEFAULT false,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS "IX_IngredientCatalog_SearchKey"
                ON familyos."IngredientCatalog" ("SearchKey");
            CREATE INDEX IF NOT EXISTS "IX_IngredientCatalog_FamilyId_Name"
                ON familyos."IngredientCatalog" ("FamilyId", "Name");

            CREATE TABLE IF NOT EXISTS familyos."RecipeIngredients" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "RecipeId" uuid NOT NULL,
                "IngredientId" uuid NOT NULL,
                "Quantity" numeric(12,3) NULL,
                "Unit" character varying(40) NULL,
                "PreparationNote" character varying(240) NULL,
                "Section" character varying(100) NULL,
                "SortOrder" integer NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS "IX_RecipeIngredients_RecipeId_SortOrder"
                ON familyos."RecipeIngredients" ("RecipeId", "SortOrder");
            CREATE INDEX IF NOT EXISTS "IX_RecipeIngredients_RecipeId_IngredientId"
                ON familyos."RecipeIngredients" ("RecipeId", "IngredientId");

            CREATE TABLE IF NOT EXISTS familyos."RecipeTags" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "FamilyId" uuid NULL,
                "Name" character varying(60) NOT NULL,
                "Slug" character varying(80) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS "IX_RecipeTags_Slug" ON familyos."RecipeTags" ("Slug");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeTags_FamilyId_Slug"
                ON familyos."RecipeTags" ("FamilyId", "Slug");

            CREATE TABLE IF NOT EXISTS familyos."RecipeTagLinks" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "RecipeId" uuid NOT NULL,
                "TagId" uuid NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeTagLinks_RecipeId_TagId"
                ON familyos."RecipeTagLinks" ("RecipeId", "TagId");

            CREATE TABLE IF NOT EXISTS familyos."RecipeInstructionSteps" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "RecipeId" uuid NOT NULL,
                "StepNumber" integer NOT NULL,
                "Text" character varying(2000) NOT NULL,
                "DurationMinutes" integer NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeInstructionSteps_RecipeId_StepNumber"
                ON familyos."RecipeInstructionSteps" ("RecipeId", "StepNumber");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO familyos."IngredientCatalog"
                ("Id", "FamilyId", "Name", "SearchKey", "Category", "AliasesCsv", "IsBuiltIn", "CreatedAtUtc", "IsDeleted")
            SELECT v."Id", NULL, v."Name", v."SearchKey", v."Category", v."AliasesCsv", true, now(), false
            FROM (VALUES
            (gen_random_uuid(), NULL, 'All-purpose flour', 'all-purpose flour', 'Pantry', 'plain flour', true, now(), false),
(gen_random_uuid(), NULL, 'Bread flour', 'bread flour', 'Pantry', 'strong flour', true, now(), false),
(gen_random_uuid(), NULL, 'Granulated sugar', 'granulated sugar', 'Pantry', 'white sugar', true, now(), false),
(gen_random_uuid(), NULL, 'Brown sugar', 'brown sugar', 'Pantry', 'light brown sugar;dark brown sugar', true, now(), false),
(gen_random_uuid(), NULL, 'Powdered sugar', 'powdered sugar', 'Pantry', 'icing sugar;confectioners sugar', true, now(), false),
(gen_random_uuid(), NULL, 'Baking powder', 'baking powder', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Baking soda', 'baking soda', 'Pantry', 'bicarbonate of soda', true, now(), false),
(gen_random_uuid(), NULL, 'Salt', 'salt', 'Spices', 'table salt', true, now(), false),
(gen_random_uuid(), NULL, 'Black pepper', 'black pepper', 'Spices', 'ground black pepper', true, now(), false),
(gen_random_uuid(), NULL, 'Olive oil', 'olive oil', 'Pantry', 'extra virgin olive oil', true, now(), false),
(gen_random_uuid(), NULL, 'Vegetable oil', 'vegetable oil', 'Pantry', 'canola oil', true, now(), false),
(gen_random_uuid(), NULL, 'Butter', 'butter', 'Dairy', 'unsalted butter;salted butter', true, now(), false),
(gen_random_uuid(), NULL, 'Eggs', 'eggs', 'Dairy', 'large eggs', true, now(), false),
(gen_random_uuid(), NULL, 'Milk', 'milk', 'Dairy', 'whole milk', true, now(), false),
(gen_random_uuid(), NULL, 'Heavy cream', 'heavy cream', 'Dairy', 'whipping cream', true, now(), false),
(gen_random_uuid(), NULL, 'Sour cream', 'sour cream', 'Dairy', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Cream cheese', 'cream cheese', 'Dairy', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Shredded cheddar cheese', 'shredded cheddar cheese', 'Dairy', 'cheddar', true, now(), false),
(gen_random_uuid(), NULL, 'Mozzarella cheese', 'mozzarella cheese', 'Dairy', 'mozzarella', true, now(), false),
(gen_random_uuid(), NULL, 'Parmesan cheese', 'parmesan cheese', 'Dairy', 'parmigiano', true, now(), false),
(gen_random_uuid(), NULL, 'Plain Greek yogurt', 'plain greek yogurt', 'Dairy', 'greek yogurt', true, now(), false),
(gen_random_uuid(), NULL, 'Chicken breast', 'chicken breast', 'Meat', 'boneless skinless chicken breast', true, now(), false),
(gen_random_uuid(), NULL, 'Chicken thighs', 'chicken thighs', 'Meat', 'boneless chicken thighs', true, now(), false),
(gen_random_uuid(), NULL, 'Ground beef', 'ground beef', 'Meat', 'minced beef', true, now(), false),
(gen_random_uuid(), NULL, 'Steak', 'steak', 'Meat', 'beef steak', true, now(), false),
(gen_random_uuid(), NULL, 'Bacon', 'bacon', 'Meat', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Italian sausage', 'italian sausage', 'Meat', 'sausage', true, now(), false),
(gen_random_uuid(), NULL, 'Ground turkey', 'ground turkey', 'Meat', 'minced turkey', true, now(), false),
(gen_random_uuid(), NULL, 'Salmon fillet', 'salmon fillet', 'Seafood', 'salmon', true, now(), false),
(gen_random_uuid(), NULL, 'Shrimp', 'shrimp', 'Seafood', 'prawns', true, now(), false),
(gen_random_uuid(), NULL, 'Yellow onion', 'yellow onion', 'Produce', 'onion', true, now(), false),
(gen_random_uuid(), NULL, 'Red onion', 'red onion', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Garlic', 'garlic', 'Produce', 'garlic cloves', true, now(), false),
(gen_random_uuid(), NULL, 'Russet potatoes', 'russet potatoes', 'Produce', 'potatoes', true, now(), false),
(gen_random_uuid(), NULL, 'Sweet potatoes', 'sweet potatoes', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Carrots', 'carrots', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Celery', 'celery', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Broccoli', 'broccoli', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Bell pepper', 'bell pepper', 'Produce', 'capsicum', true, now(), false),
(gen_random_uuid(), NULL, 'Tomatoes', 'tomatoes', 'Produce', 'fresh tomato', true, now(), false),
(gen_random_uuid(), NULL, 'Cherry tomatoes', 'cherry tomatoes', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Cucumber', 'cucumber', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Spinach', 'spinach', 'Produce', 'baby spinach', true, now(), false),
(gen_random_uuid(), NULL, 'Lettuce', 'lettuce', 'Produce', 'romaine lettuce', true, now(), false),
(gen_random_uuid(), NULL, 'Avocado', 'avocado', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Lemon', 'lemon', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Lime', 'lime', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Fresh parsley', 'fresh parsley', 'Produce', 'parsley', true, now(), false),
(gen_random_uuid(), NULL, 'Fresh cilantro', 'fresh cilantro', 'Produce', 'coriander leaves', true, now(), false),
(gen_random_uuid(), NULL, 'Fresh basil', 'fresh basil', 'Produce', 'basil', true, now(), false),
(gen_random_uuid(), NULL, 'Apples', 'apples', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Bananas', 'bananas', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Blueberries', 'blueberries', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Strawberries', 'strawberries', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Linguine', 'linguine', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Spaghetti', 'spaghetti', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Penne pasta', 'penne pasta', 'Pantry', 'penne', true, now(), false),
(gen_random_uuid(), NULL, 'Macaroni', 'macaroni', 'Pantry', 'elbow macaroni', true, now(), false),
(gen_random_uuid(), NULL, 'White rice', 'white rice', 'Pantry', 'long grain rice', true, now(), false),
(gen_random_uuid(), NULL, 'Brown rice', 'brown rice', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Quinoa', 'quinoa', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Rolled oats', 'rolled oats', 'Pantry', 'old fashioned oats', true, now(), false),
(gen_random_uuid(), NULL, 'Breadcrumbs', 'breadcrumbs', 'Pantry', 'bread crumbs', true, now(), false),
(gen_random_uuid(), NULL, 'Panko breadcrumbs', 'panko breadcrumbs', 'Pantry', 'panko', true, now(), false),
(gen_random_uuid(), NULL, 'Tortillas', 'tortillas', 'Bakery', 'flour tortillas', true, now(), false),
(gen_random_uuid(), NULL, 'Sandwich bread', 'sandwich bread', 'Bakery', 'bread', true, now(), false),
(gen_random_uuid(), NULL, 'Hamburger buns', 'hamburger buns', 'Bakery', 'burger buns', true, now(), false),
(gen_random_uuid(), NULL, 'Pita bread', 'pita bread', 'Bakery', 'pita', true, now(), false),
(gen_random_uuid(), NULL, 'Canned diced tomatoes', 'canned diced tomatoes', 'Canned', 'tinned chopped tomatoes', true, now(), false),
(gen_random_uuid(), NULL, 'Tomato paste', 'tomato paste', 'Canned', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Marinara sauce', 'marinara sauce', 'Canned', 'pasta sauce', true, now(), false),
(gen_random_uuid(), NULL, 'Chicken broth', 'chicken broth', 'Canned', 'chicken stock', true, now(), false),
(gen_random_uuid(), NULL, 'Beef broth', 'beef broth', 'Canned', 'beef stock', true, now(), false),
(gen_random_uuid(), NULL, 'Black beans', 'black beans', 'Canned', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Kidney beans', 'kidney beans', 'Canned', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Chickpeas', 'chickpeas', 'Canned', 'garbanzo beans', true, now(), false),
(gen_random_uuid(), NULL, 'Corn', 'corn', 'Canned', 'sweetcorn', true, now(), false),
(gen_random_uuid(), NULL, 'Peanut butter', 'peanut butter', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Honey', 'honey', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Maple syrup', 'maple syrup', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Soy sauce', 'soy sauce', 'Condiments', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Worcestershire sauce', 'worcestershire sauce', 'Condiments', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Dijon mustard', 'dijon mustard', 'Condiments', 'mustard', true, now(), false),
(gen_random_uuid(), NULL, 'Mayonnaise', 'mayonnaise', 'Condiments', 'mayo', true, now(), false),
(gen_random_uuid(), NULL, 'Ketchup', 'ketchup', 'Condiments', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'BBQ sauce', 'bbq sauce', 'Condiments', 'barbecue sauce', true, now(), false),
(gen_random_uuid(), NULL, 'Hot sauce', 'hot sauce', 'Condiments', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Italian seasoning', 'italian seasoning', 'Spices', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Paprika', 'paprika', 'Spices', 'smoked paprika', true, now(), false),
(gen_random_uuid(), NULL, 'Chili powder', 'chili powder', 'Spices', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Ground cumin', 'ground cumin', 'Spices', 'cumin', true, now(), false),
(gen_random_uuid(), NULL, 'Dried oregano', 'dried oregano', 'Spices', 'oregano', true, now(), false),
(gen_random_uuid(), NULL, 'Dried thyme', 'dried thyme', 'Spices', 'thyme', true, now(), false),
(gen_random_uuid(), NULL, 'Cinnamon', 'cinnamon', 'Spices', 'ground cinnamon', true, now(), false),
(gen_random_uuid(), NULL, 'Red pepper flakes', 'red pepper flakes', 'Spices', 'crushed red pepper', true, now(), false),
(gen_random_uuid(), NULL, 'Vanilla extract', 'vanilla extract', 'Baking', 'vanilla', true, now(), false),
(gen_random_uuid(), NULL, 'Cocoa powder', 'cocoa powder', 'Baking', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Chocolate chips', 'chocolate chips', 'Baking', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Cornstarch', 'cornstarch', 'Baking', 'corn flour', true, now(), false),
(gen_random_uuid(), NULL, 'Active dry yeast', 'active dry yeast', 'Baking', 'yeast', true, now(), false),
(gen_random_uuid(), NULL, 'Frozen peas', 'frozen peas', 'Frozen', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Frozen mixed vegetables', 'frozen mixed vegetables', 'Frozen', 'mixed veg', true, now(), false),
(gen_random_uuid(), NULL, 'Frozen berries', 'frozen berries', 'Frozen', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Orange juice', 'orange juice', 'Beverages', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Chicken stock', 'chicken stock', 'Canned', 'stock', true, now(), false)
            ) AS v("Id", "FamilyId", "Name", "SearchKey", "Category", "AliasesCsv", "IsBuiltIn", "CreatedAtUtc", "IsDeleted")
            WHERE NOT EXISTS (
                SELECT 1 FROM familyos."IngredientCatalog" i
                WHERE i."FamilyId" IS NULL AND i."SearchKey" = v."SearchKey" AND i."IsDeleted" = false
            );
            """.Replace("(gen_random_uuid(), NULL, 'All-purpose flour', 'all-purpose flour', 'Pantry', 'plain flour', true, now(), false),
(gen_random_uuid(), NULL, 'Bread flour', 'bread flour', 'Pantry', 'strong flour', true, now(), false),
(gen_random_uuid(), NULL, 'Granulated sugar', 'granulated sugar', 'Pantry', 'white sugar', true, now(), false),
(gen_random_uuid(), NULL, 'Brown sugar', 'brown sugar', 'Pantry', 'light brown sugar;dark brown sugar', true, now(), false),
(gen_random_uuid(), NULL, 'Powdered sugar', 'powdered sugar', 'Pantry', 'icing sugar;confectioners sugar', true, now(), false),
(gen_random_uuid(), NULL, 'Baking powder', 'baking powder', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Baking soda', 'baking soda', 'Pantry', 'bicarbonate of soda', true, now(), false),
(gen_random_uuid(), NULL, 'Salt', 'salt', 'Spices', 'table salt', true, now(), false),
(gen_random_uuid(), NULL, 'Black pepper', 'black pepper', 'Spices', 'ground black pepper', true, now(), false),
(gen_random_uuid(), NULL, 'Olive oil', 'olive oil', 'Pantry', 'extra virgin olive oil', true, now(), false),
(gen_random_uuid(), NULL, 'Vegetable oil', 'vegetable oil', 'Pantry', 'canola oil', true, now(), false),
(gen_random_uuid(), NULL, 'Butter', 'butter', 'Dairy', 'unsalted butter;salted butter', true, now(), false),
(gen_random_uuid(), NULL, 'Eggs', 'eggs', 'Dairy', 'large eggs', true, now(), false),
(gen_random_uuid(), NULL, 'Milk', 'milk', 'Dairy', 'whole milk', true, now(), false),
(gen_random_uuid(), NULL, 'Heavy cream', 'heavy cream', 'Dairy', 'whipping cream', true, now(), false),
(gen_random_uuid(), NULL, 'Sour cream', 'sour cream', 'Dairy', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Cream cheese', 'cream cheese', 'Dairy', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Shredded cheddar cheese', 'shredded cheddar cheese', 'Dairy', 'cheddar', true, now(), false),
(gen_random_uuid(), NULL, 'Mozzarella cheese', 'mozzarella cheese', 'Dairy', 'mozzarella', true, now(), false),
(gen_random_uuid(), NULL, 'Parmesan cheese', 'parmesan cheese', 'Dairy', 'parmigiano', true, now(), false),
(gen_random_uuid(), NULL, 'Plain Greek yogurt', 'plain greek yogurt', 'Dairy', 'greek yogurt', true, now(), false),
(gen_random_uuid(), NULL, 'Chicken breast', 'chicken breast', 'Meat', 'boneless skinless chicken breast', true, now(), false),
(gen_random_uuid(), NULL, 'Chicken thighs', 'chicken thighs', 'Meat', 'boneless chicken thighs', true, now(), false),
(gen_random_uuid(), NULL, 'Ground beef', 'ground beef', 'Meat', 'minced beef', true, now(), false),
(gen_random_uuid(), NULL, 'Steak', 'steak', 'Meat', 'beef steak', true, now(), false),
(gen_random_uuid(), NULL, 'Bacon', 'bacon', 'Meat', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Italian sausage', 'italian sausage', 'Meat', 'sausage', true, now(), false),
(gen_random_uuid(), NULL, 'Ground turkey', 'ground turkey', 'Meat', 'minced turkey', true, now(), false),
(gen_random_uuid(), NULL, 'Salmon fillet', 'salmon fillet', 'Seafood', 'salmon', true, now(), false),
(gen_random_uuid(), NULL, 'Shrimp', 'shrimp', 'Seafood', 'prawns', true, now(), false),
(gen_random_uuid(), NULL, 'Yellow onion', 'yellow onion', 'Produce', 'onion', true, now(), false),
(gen_random_uuid(), NULL, 'Red onion', 'red onion', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Garlic', 'garlic', 'Produce', 'garlic cloves', true, now(), false),
(gen_random_uuid(), NULL, 'Russet potatoes', 'russet potatoes', 'Produce', 'potatoes', true, now(), false),
(gen_random_uuid(), NULL, 'Sweet potatoes', 'sweet potatoes', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Carrots', 'carrots', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Celery', 'celery', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Broccoli', 'broccoli', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Bell pepper', 'bell pepper', 'Produce', 'capsicum', true, now(), false),
(gen_random_uuid(), NULL, 'Tomatoes', 'tomatoes', 'Produce', 'fresh tomato', true, now(), false),
(gen_random_uuid(), NULL, 'Cherry tomatoes', 'cherry tomatoes', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Cucumber', 'cucumber', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Spinach', 'spinach', 'Produce', 'baby spinach', true, now(), false),
(gen_random_uuid(), NULL, 'Lettuce', 'lettuce', 'Produce', 'romaine lettuce', true, now(), false),
(gen_random_uuid(), NULL, 'Avocado', 'avocado', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Lemon', 'lemon', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Lime', 'lime', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Fresh parsley', 'fresh parsley', 'Produce', 'parsley', true, now(), false),
(gen_random_uuid(), NULL, 'Fresh cilantro', 'fresh cilantro', 'Produce', 'coriander leaves', true, now(), false),
(gen_random_uuid(), NULL, 'Fresh basil', 'fresh basil', 'Produce', 'basil', true, now(), false),
(gen_random_uuid(), NULL, 'Apples', 'apples', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Bananas', 'bananas', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Blueberries', 'blueberries', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Strawberries', 'strawberries', 'Produce', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Linguine', 'linguine', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Spaghetti', 'spaghetti', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Penne pasta', 'penne pasta', 'Pantry', 'penne', true, now(), false),
(gen_random_uuid(), NULL, 'Macaroni', 'macaroni', 'Pantry', 'elbow macaroni', true, now(), false),
(gen_random_uuid(), NULL, 'White rice', 'white rice', 'Pantry', 'long grain rice', true, now(), false),
(gen_random_uuid(), NULL, 'Brown rice', 'brown rice', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Quinoa', 'quinoa', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Rolled oats', 'rolled oats', 'Pantry', 'old fashioned oats', true, now(), false),
(gen_random_uuid(), NULL, 'Breadcrumbs', 'breadcrumbs', 'Pantry', 'bread crumbs', true, now(), false),
(gen_random_uuid(), NULL, 'Panko breadcrumbs', 'panko breadcrumbs', 'Pantry', 'panko', true, now(), false),
(gen_random_uuid(), NULL, 'Tortillas', 'tortillas', 'Bakery', 'flour tortillas', true, now(), false),
(gen_random_uuid(), NULL, 'Sandwich bread', 'sandwich bread', 'Bakery', 'bread', true, now(), false),
(gen_random_uuid(), NULL, 'Hamburger buns', 'hamburger buns', 'Bakery', 'burger buns', true, now(), false),
(gen_random_uuid(), NULL, 'Pita bread', 'pita bread', 'Bakery', 'pita', true, now(), false),
(gen_random_uuid(), NULL, 'Canned diced tomatoes', 'canned diced tomatoes', 'Canned', 'tinned chopped tomatoes', true, now(), false),
(gen_random_uuid(), NULL, 'Tomato paste', 'tomato paste', 'Canned', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Marinara sauce', 'marinara sauce', 'Canned', 'pasta sauce', true, now(), false),
(gen_random_uuid(), NULL, 'Chicken broth', 'chicken broth', 'Canned', 'chicken stock', true, now(), false),
(gen_random_uuid(), NULL, 'Beef broth', 'beef broth', 'Canned', 'beef stock', true, now(), false),
(gen_random_uuid(), NULL, 'Black beans', 'black beans', 'Canned', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Kidney beans', 'kidney beans', 'Canned', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Chickpeas', 'chickpeas', 'Canned', 'garbanzo beans', true, now(), false),
(gen_random_uuid(), NULL, 'Corn', 'corn', 'Canned', 'sweetcorn', true, now(), false),
(gen_random_uuid(), NULL, 'Peanut butter', 'peanut butter', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Honey', 'honey', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Maple syrup', 'maple syrup', 'Pantry', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Soy sauce', 'soy sauce', 'Condiments', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Worcestershire sauce', 'worcestershire sauce', 'Condiments', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Dijon mustard', 'dijon mustard', 'Condiments', 'mustard', true, now(), false),
(gen_random_uuid(), NULL, 'Mayonnaise', 'mayonnaise', 'Condiments', 'mayo', true, now(), false),
(gen_random_uuid(), NULL, 'Ketchup', 'ketchup', 'Condiments', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'BBQ sauce', 'bbq sauce', 'Condiments', 'barbecue sauce', true, now(), false),
(gen_random_uuid(), NULL, 'Hot sauce', 'hot sauce', 'Condiments', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Italian seasoning', 'italian seasoning', 'Spices', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Paprika', 'paprika', 'Spices', 'smoked paprika', true, now(), false),
(gen_random_uuid(), NULL, 'Chili powder', 'chili powder', 'Spices', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Ground cumin', 'ground cumin', 'Spices', 'cumin', true, now(), false),
(gen_random_uuid(), NULL, 'Dried oregano', 'dried oregano', 'Spices', 'oregano', true, now(), false),
(gen_random_uuid(), NULL, 'Dried thyme', 'dried thyme', 'Spices', 'thyme', true, now(), false),
(gen_random_uuid(), NULL, 'Cinnamon', 'cinnamon', 'Spices', 'ground cinnamon', true, now(), false),
(gen_random_uuid(), NULL, 'Red pepper flakes', 'red pepper flakes', 'Spices', 'crushed red pepper', true, now(), false),
(gen_random_uuid(), NULL, 'Vanilla extract', 'vanilla extract', 'Baking', 'vanilla', true, now(), false),
(gen_random_uuid(), NULL, 'Cocoa powder', 'cocoa powder', 'Baking', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Chocolate chips', 'chocolate chips', 'Baking', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Cornstarch', 'cornstarch', 'Baking', 'corn flour', true, now(), false),
(gen_random_uuid(), NULL, 'Active dry yeast', 'active dry yeast', 'Baking', 'yeast', true, now(), false),
(gen_random_uuid(), NULL, 'Frozen peas', 'frozen peas', 'Frozen', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Frozen mixed vegetables', 'frozen mixed vegetables', 'Frozen', 'mixed veg', true, now(), false),
(gen_random_uuid(), NULL, 'Frozen berries', 'frozen berries', 'Frozen', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Orange juice', 'orange juice', 'Beverages', NULL, true, now(), false),
(gen_random_uuid(), NULL, 'Chicken stock', 'chicken stock', 'Canned', 'stock', true, now(), false)", ""));
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO familyos."RecipeTags" ("Id", "FamilyId", "Name", "Slug", "CreatedAtUtc", "IsDeleted")
            SELECT v."Id", NULL, v."Name", v."Slug", now(), false
            FROM (VALUES
            (gen_random_uuid(), NULL, 'Breakfast', 'breakfast', now(), false),
(gen_random_uuid(), NULL, 'Lunch', 'lunch', now(), false),
(gen_random_uuid(), NULL, 'Dinner', 'dinner', now(), false),
(gen_random_uuid(), NULL, 'Snack', 'snack', now(), false),
(gen_random_uuid(), NULL, 'Dessert', 'dessert', now(), false),
(gen_random_uuid(), NULL, 'Vegetarian', 'vegetarian', now(), false),
(gen_random_uuid(), NULL, 'Vegan', 'vegan', now(), false),
(gen_random_uuid(), NULL, 'Gluten-Free', 'gluten-free', now(), false),
(gen_random_uuid(), NULL, 'Dairy-Free', 'dairy-free', now(), false),
(gen_random_uuid(), NULL, 'High-Protein', 'high-protein', now(), false),
(gen_random_uuid(), NULL, 'Low-Carb', 'low-carb', now(), false),
(gen_random_uuid(), NULL, 'Budget-Friendly', 'budget-friendly', now(), false),
(gen_random_uuid(), NULL, 'Kid-Friendly', 'kid-friendly', now(), false),
(gen_random_uuid(), NULL, 'Quick', 'quick', now(), false),
(gen_random_uuid(), NULL, 'Under-30-Minutes', 'under-30-minutes', now(), false),
(gen_random_uuid(), NULL, 'One-Pot', 'one-pot', now(), false),
(gen_random_uuid(), NULL, 'Slow-Cooker', 'slow-cooker', now(), false),
(gen_random_uuid(), NULL, 'Air-Fryer', 'air-fryer', now(), false),
(gen_random_uuid(), NULL, 'Meal-Prep', 'meal-prep', now(), false),
(gen_random_uuid(), NULL, 'Freezer-Friendly', 'freezer-friendly', now(), false),
(gen_random_uuid(), NULL, 'Comfort-Food', 'comfort-food', now(), false),
(gen_random_uuid(), NULL, 'Italian', 'italian', now(), false),
(gen_random_uuid(), NULL, 'Mexican', 'mexican', now(), false),
(gen_random_uuid(), NULL, 'American', 'american', now(), false),
(gen_random_uuid(), NULL, 'Asian-Inspired', 'asian-inspired', now(), false),
(gen_random_uuid(), NULL, 'Mediterranean', 'mediterranean', now(), false),
(gen_random_uuid(), NULL, 'Healthy', 'healthy', now(), false),
(gen_random_uuid(), NULL, 'Family-Favorite', 'family-favorite', now(), false)
            ) AS v("Id", "FamilyId", "Name", "Slug", "CreatedAtUtc", "IsDeleted")
            WHERE NOT EXISTS (
                SELECT 1 FROM familyos."RecipeTags" t
                WHERE t."FamilyId" IS NULL AND t."Slug" = v."Slug" AND t."IsDeleted" = false
            );
            """.Replace("(gen_random_uuid(), NULL, 'Breakfast', 'breakfast', now(), false),
(gen_random_uuid(), NULL, 'Lunch', 'lunch', now(), false),
(gen_random_uuid(), NULL, 'Dinner', 'dinner', now(), false),
(gen_random_uuid(), NULL, 'Snack', 'snack', now(), false),
(gen_random_uuid(), NULL, 'Dessert', 'dessert', now(), false),
(gen_random_uuid(), NULL, 'Vegetarian', 'vegetarian', now(), false),
(gen_random_uuid(), NULL, 'Vegan', 'vegan', now(), false),
(gen_random_uuid(), NULL, 'Gluten-Free', 'gluten-free', now(), false),
(gen_random_uuid(), NULL, 'Dairy-Free', 'dairy-free', now(), false),
(gen_random_uuid(), NULL, 'High-Protein', 'high-protein', now(), false),
(gen_random_uuid(), NULL, 'Low-Carb', 'low-carb', now(), false),
(gen_random_uuid(), NULL, 'Budget-Friendly', 'budget-friendly', now(), false),
(gen_random_uuid(), NULL, 'Kid-Friendly', 'kid-friendly', now(), false),
(gen_random_uuid(), NULL, 'Quick', 'quick', now(), false),
(gen_random_uuid(), NULL, 'Under-30-Minutes', 'under-30-minutes', now(), false),
(gen_random_uuid(), NULL, 'One-Pot', 'one-pot', now(), false),
(gen_random_uuid(), NULL, 'Slow-Cooker', 'slow-cooker', now(), false),
(gen_random_uuid(), NULL, 'Air-Fryer', 'air-fryer', now(), false),
(gen_random_uuid(), NULL, 'Meal-Prep', 'meal-prep', now(), false),
(gen_random_uuid(), NULL, 'Freezer-Friendly', 'freezer-friendly', now(), false),
(gen_random_uuid(), NULL, 'Comfort-Food', 'comfort-food', now(), false),
(gen_random_uuid(), NULL, 'Italian', 'italian', now(), false),
(gen_random_uuid(), NULL, 'Mexican', 'mexican', now(), false),
(gen_random_uuid(), NULL, 'American', 'american', now(), false),
(gen_random_uuid(), NULL, 'Asian-Inspired', 'asian-inspired', now(), false),
(gen_random_uuid(), NULL, 'Mediterranean', 'mediterranean', now(), false),
(gen_random_uuid(), NULL, 'Healthy', 'healthy', now(), false),
(gen_random_uuid(), NULL, 'Family-Favorite', 'family-favorite', now(), false)", ""));
        logger.LogInformation("Recipe catalog schema ensured; seeded {IngredientCount} common ingredients and {TagCount} tags.",
            105, 28);
    }
}
