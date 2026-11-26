# FamilyOS Meal Planning & Recipe Library V2

## Goal

Upgrade the existing MAUI meal planner into a family meal-planning system with a large built-in recipe collection, fast recipe entry, a reusable ingredient catalog, recipe import, searchable tags, and accurate shopping lists.

The current implementation already supports weekly meal plans, family recipes, applying recipes to a day/slot, scaling servings, and sending ingredient strings to procurement. Recipe and meal ingredients are currently stored as comma-separated text. V2 must preserve this existing data while introducing structured ingredients.

## Product principles

- Make common actions fast: add a recipe, add a meal to a day, and create a shopping list should take very few taps.
- Prefer selection and autocomplete over repeated typing.
- Keep the experience simple and visually consistent with the dark sci-fi MAUI design system.
- Built-in recipes are shared catalog content; family-created recipes remain family-owned.
- Never silently overwrite a family's recipe with imported content.
- Shopping lists should be generated from planned recipes and optionally a selected date range, with a review step before adding items to the household shopping queue.

## Main experiences

### 1. Recipe discovery

- Search by recipe name, ingredient, cuisine, meal type, dietary tag, preparation time, and difficulty.
- Filter chips for Breakfast, Lunch, Dinner, Snack, Vegetarian, Vegan, Gluten-Free, Dairy-Free, High-Protein, Budget-Friendly, Kid-Friendly, Quick, Slow Cooker, One-Pot, and similar tags.
- Sort by relevance, preparation time, newest, and family favorites.
- Show servings, prep/cook/total time, tags, and a compact ingredient preview on recipe cards.
- Include built-in recipes and family recipes in one searchable library, clearly labelled by source.

### 2. Ingredient catalog and recipe editor

- Searchable catalog with canonical ingredient names, aliases, category, and common units.
- Categories include produce, meat/seafood, dairy/eggs, bakery, pantry, spices, frozen, beverages, and household staples.
- Ingredient rows use: ingredient, quantity, unit, preparation note, and optional section.
- Support fractional quantities (1/2, 1/3, 1 1/2), decimals, count units, weight, and volume.
- Quick-add an ingredient not found in the catalog; offer to add it to the family's custom ingredient catalog.
- Reorder ingredients and group them into sections.
- Instructions support ordered steps.
- Preserve legacy IngredientsCsv on read during migration; convert recognizable entries to structured ingredients without dropping unparsed text.

### 3. Recipe import from the web

- Accept a recipe URL, fetch the page server-side, and extract recipe metadata using schema.org Recipe JSON-LD first.
- Parse name, description, image, servings, prep/cook times, ingredients, instructions, cuisine, and tags when available.
- Show a review/edit screen before saving. Never auto-publish or silently replace a recipe.
- Store source URL and attribution metadata.
- If extraction fails, show a friendly fallback for manual entry or paste-in text.
- Respect website access restrictions and avoid scraping behind logins/paywalls.

### 4. Meal planner

- Weekly calendar with breakfast, lunch, dinner, and snack slots.
- Add from recipe library, repeat a previous meal, or enter a quick custom meal.
- Scale recipe servings to household needs.
- Make it easy to move a meal to another day/slot.
- Support generating a list for the whole week or selected individual days.

### 5. Shopping list generation

- Build a preview from selected planned meals.
- Aggregate identical catalog ingredients and compatible units; convert only safe, known conversions.
- Do not combine incompatible units or ambiguous ingredient variants.
- Keep preparation notes and recipe references where useful.
- Allow pantry exclusions, quantity edits, and item removal before adding to the existing family shopping queue.
- Mark items already on the active shopping queue to avoid duplicates.
- Keep the existing procurement/shopping workflow as the destination.

## Data model direction

Introduce normalized records rather than extending comma-separated strings:

- Ingredient: canonical name, normalized search key, category, default unit, aliases, source (built-in/family), and optional family ownership.
- Recipe: existing family-owned recipe plus source (built-in/family/imported), description, image/source URL, prep/cook time, difficulty, cuisine, and publication/visibility metadata.
- RecipeIngredient: recipe ID, ingredient ID, quantity decimal, unit, preparation note, sort order, and optional section.
- RecipeTag and RecipeTagLink: normalized reusable tags and recipe associations.
- RecipeInstructionStep: recipe ID, step number, instruction text, optional duration.
- Built-in catalog content must be idempotently seeded and must not be copied into every family's private recipe rows.
- Existing Recipes.IngredientsCsv remains readable during transition; new writes use structured rows.

## API direction

- Search/filter built-in and family recipes with paging.
- Retrieve recipe details including structured ingredient rows, steps, and tags.
- Search ingredient catalog with prefix/alias matching.
- Create/update family recipes with structured ingredients and tags.
- Import recipe from URL into a reviewable draft.
- Preview shopping aggregation for a week or explicit dates.
- Confirm adding the reviewed shopping list to procurement.
- Keep family authorization on every family-owned recipe and shopping operation.

## Delivery sequence

1. **Foundation:** schema and domain model for ingredients, structured recipe ingredients, tags, instruction steps, and source metadata; preserve legacy recipes.
2. **Seed catalog:** add a substantial curated built-in starter collection and ingredient catalog, seeded idempotently. Grow the recipe catalog in data batches without changing the app code.
3. **Recipe discovery/editor:** search, filters, tags, ingredient picker, measurements, and structured recipe editing in MAUI.
4. **Meal planner integration:** select recipes into meal slots, scale servings, and retain recipe linkage for shopping.
5. **Shopping preview:** day/week selection, ingredient aggregation, review/edit, and safe transfer to the existing shopping queue.
6. **Web import:** URL extraction, review/edit, attribution, and manual fallback.
7. **Tests and release:** migration/upgrade tests for existing CSV recipes, quantity/unit aggregation tests, API authorization tests, and Android release build.

## Acceptance criteria

- Existing family recipes and meal plans still load after deployment.
- A user can add a recipe by selecting catalog ingredients and entering measurements without typing a comma-separated ingredient paragraph.
- Ingredient search recognizes common aliases and returns useful results quickly.
- Recipes can be filtered by multiple tags.
- A built-in recipe can be added to the meal plan without creating a duplicate private copy.
- A shopping list can be generated for one selected day or a full week.
- Duplicate ingredients are combined only when their units are compatible and conversion is reliable.
- The user reviews the shopping list before it is sent to the household queue.
- Importing a URL creates an editable draft and retains source attribution.
