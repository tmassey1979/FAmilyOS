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
        "Breakfast", "Lunch", "Dinner", "Side", "Snack", "Dessert", "No-Bake", "Vegetarian", "Vegan", "Gluten-Free",
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
            ["Breakfast", "Vegetarian", "Meal-Prep", "Quick"]),
        new("Classic French toast", 4, "8 slices sandwich bread, 4 eggs, 1 cup milk, 1 tsp cinnamon, 1 tsp vanilla extract, 2 tbsp butter", "Whisk eggs, milk, cinnamon, and vanilla. Dip bread and cook in buttered skillet until golden on both sides.", ["Breakfast", "Kid-Friendly", "Quick"]),
        new("Sausage breakfast skillet", 4, "1 lb Italian sausage, 4 russet potatoes, 1 bell pepper, 1 onion, 4 eggs", "Brown sausage. Add diced potatoes, pepper, and onion; cover and cook until tender. Top with fried eggs.", ["Breakfast", "One-Pot", "High-Protein"]),
        new("Ham and cheese breakfast burritos", 4, "4 tortillas, 6 eggs, 1 cup shredded cheddar cheese, 1 cup ham, 1 tbsp butter", "Scramble eggs in butter. Fill warm tortillas with eggs, ham, and cheese; roll tightly.", ["Breakfast", "Meal-Prep", "Kid-Friendly"]),
        new("Peanut butter banana toast", 2, "4 slices sandwich bread, 4 tbsp peanut butter, 2 bananas, 1 tbsp honey", "Toast bread. Spread with peanut butter, top with sliced banana, and drizzle with honey.", ["Breakfast", "Quick", "Kid-Friendly"]),
        new("Sausage gravy and biscuits", 4, "1 lb Italian sausage, 1/4 cup all-purpose flour, 3 cups milk, 8 biscuits, 1 tsp black pepper", "Brown sausage. Stir in flour, then gradually whisk in milk. Simmer until thick and spoon over warm biscuits.", ["Breakfast", "Comfort-Food", "Family-Favorite"]),
        new("Ham and spinach frittata", 4, "8 eggs, 1/2 cup milk, 1 cup ham, 1 cup spinach, 1 cup shredded cheddar cheese", "Whisk eggs and milk. Stir in ham, spinach, and cheese. Bake in oven-safe skillet at 375 F until set.", ["Breakfast", "High-Protein", "Gluten-Free"]),
        new("Cinnamon apple baked oatmeal", 6, "2 cups rolled oats, 2 apples, 2 eggs, 2 cups milk, 1 tsp cinnamon, 1/4 cup brown sugar", "Mix all ingredients and pour into greased baking dish. Bake at 350 F for 35-40 minutes.", ["Breakfast", "Meal-Prep", "Vegetarian"]),
        new("Breakfast quesadillas", 4, "4 tortillas, 6 eggs, 1 cup shredded cheddar cheese, 1/2 bell pepper, 2 tbsp butter", "Scramble eggs with diced pepper. Fill tortillas with eggs and cheese; toast in buttered skillet until crisp.", ["Breakfast", "Quick", "Kid-Friendly"]),
        new("Chicken Caesar wraps", 4, "2 cups cooked chicken, 4 tortillas, 2 cups lettuce, 1/2 cup parmesan cheese, 1/2 cup mayonnaise", "Toss chopped chicken and lettuce with mayonnaise and parmesan. Roll into tortillas and serve.", ["Lunch", "Quick", "High-Protein"]),
        new("BLT sandwiches", 4, "8 slices sandwich bread, 8 slices bacon, 2 tomatoes, 4 leaves lettuce, 1/4 cup mayonnaise", "Cook bacon until crisp. Toast bread and spread with mayonnaise. Layer bacon, tomato, and lettuce.", ["Lunch", "Quick", "Family-Favorite"]),
        new("Chicken salad sandwiches", 4, "2 cups cooked chicken, 1/2 cup mayonnaise, 2 celery stalks, 1/4 red onion, 8 slices sandwich bread", "Chop chicken, celery, and onion. Mix with mayonnaise and season to taste. Serve on bread.", ["Lunch", "Meal-Prep", "Budget-Friendly"]),
        new("Ham and cheddar sliders", 6, "12 slider buns, 12 slices ham, 12 slices cheddar cheese, 3 tbsp butter, 1 tbsp Dijon mustard", "Layer ham and cheese in buns. Brush tops with melted butter and mustard. Bake covered at 350 F for 15 minutes.", ["Lunch", "Kid-Friendly", "Family-Favorite"]),
        new("Tuna melts", 4, "2 cans tuna, 1/4 cup mayonnaise, 8 slices sandwich bread, 4 slices cheddar cheese, 2 tbsp butter", "Mix tuna and mayonnaise. Spread on bread, top with cheese, and cook in buttered skillet until golden.", ["Lunch", "Quick", "Budget-Friendly"]),
        new("Caprese pasta salad", 6, "12 oz penne pasta, 1 cup cherry tomatoes, 1 cup mozzarella cheese, 1/4 cup fresh basil, 2 tbsp olive oil", "Cook pasta and cool. Toss with tomatoes, mozzarella, basil, and olive oil. Chill before serving.", ["Lunch", "Vegetarian", "Meal-Prep"]),
        new("Southwest chicken salad", 4, "2 cups cooked chicken, 4 cups lettuce, 1 can black beans, 1 cup corn, 1 tomato, 1/2 cup shredded cheddar cheese", "Chop lettuce and tomato. Top with chicken, beans, corn, and cheese. Serve with your favorite dressing.", ["Lunch", "High-Protein", "Gluten-Free"]),
        new("Grilled cheese and tomato", 2, "4 slices sandwich bread, 4 slices cheddar cheese, 2 tbsp butter, 1 tomato", "Layer cheese and tomato between bread. Butter outside and grill until bread is golden and cheese melts.", ["Lunch", "Vegetarian", "Quick", "Kid-Friendly"]),
        new("Easy chicken ranch wraps", 4, "2 cups cooked chicken, 4 tortillas, 2 cups lettuce, 1/2 cup sour cream, 1 tsp garlic", "Mix sour cream and garlic. Fill tortillas with chicken, lettuce, and sauce; roll and serve.", ["Lunch", "Quick", "Kid-Friendly"]),
        new("Turkey club sandwiches", 4, "8 slices sandwich bread, 8 slices turkey, 8 slices bacon, 1 tomato, 4 leaves lettuce, 1/4 cup mayonnaise", "Toast bread. Spread with mayonnaise and layer turkey, bacon, tomato, and lettuce into sandwiches.", ["Lunch", "Quick", "High-Protein"]),
        new("Easy beef enchiladas", 6, "1 lb ground beef, 8 tortillas, 2 cups enchilada sauce, 2 cups shredded cheddar cheese, 1 onion", "Brown beef with onion. Fill tortillas with beef and cheese, roll into baking dish, cover with sauce and cheese. Bake at 375 F for 20 minutes.", ["Dinner", "Mexican", "Family-Favorite"]),
        new("Chicken parmesan", 4, "4 chicken breast portions, 1 cup breadcrumbs, 1 egg, 2 cups marinara sauce, 1 cup mozzarella cheese, 1/4 cup parmesan cheese", "Coat chicken in egg and breadcrumbs. Bake at 400 F until cooked through. Top with marinara and cheeses; bake until melted.", ["Dinner", "Italian", "Family-Favorite"]),
        new("Beef stroganoff", 4, "1 lb steak, 8 oz linguine, 1 onion, 2 cups beef broth, 1 cup sour cream, 1 tbsp olive oil", "Cook pasta. Sear sliced steak and onion in oil. Add broth and simmer; remove from heat and stir in sour cream. Serve over pasta.", ["Dinner", "Comfort-Food", "Family-Favorite"]),
        new("Pork chops with roasted potatoes", 4, "4 pork chops, 1 lb russet potatoes, 2 tbsp olive oil, 1 tsp paprika, 1 tsp garlic", "Season pork and potatoes with oil and spices. Roast potatoes at 425 F; add pork chops and cook until safe internal temperature.", ["Dinner", "One-Pot", "Gluten-Free"]),
        new("Sausage and peppers", 4, "1 lb Italian sausage, 2 bell peppers, 1 onion, 1 tbsp olive oil, 4 sandwich rolls", "Brown sausage. Add sliced peppers and onion; sauté until tender. Serve in warm rolls.", ["Dinner", "Budget-Friendly", "Quick"]),
        new("Chicken broccoli rice casserole", 6, "2 cups cooked chicken, 2 cups white rice, 2 cups broccoli, 1 cup milk, 1 cup shredded cheddar cheese, 1 cup chicken broth", "Combine chicken, rice, broccoli, milk, broth, and half the cheese. Bake covered at 375 F for 25 minutes; top with remaining cheese.", ["Dinner", "Comfort-Food", "One-Pot"]),
        new("Stuffed bell peppers", 4, "4 bell peppers, 1 lb ground beef, 1 cup white rice, 1 cup marinara sauce, 1 cup mozzarella cheese", "Brown beef and mix with cooked rice and marinara. Fill halved peppers, top with cheese, and bake at 375 F until peppers are tender.", ["Dinner", "Meal-Prep", "Gluten-Free"]),
        new("Creamy mushroom chicken", 4, "1 lb chicken breast, 8 oz mushrooms, 1 cup heavy cream, 1 onion, 1 tbsp olive oil, 1 tsp garlic", "Sear chicken in oil. Sauté onion and mushrooms, add garlic and cream, then return chicken and simmer until cooked through.", ["Dinner", "Comfort-Food", "High-Protein"]),
        new("Cajun chicken pasta", 4, "1 lb chicken breast, 12 oz penne pasta, 1 cup heavy cream, 1 bell pepper, 2 tsp paprika, 1 tsp chili powder", "Cook pasta. Season and sauté chicken with pepper. Add cream and spices, simmer, then toss with pasta.", ["Dinner", "Quick", "High-Protein"]),
        new("Teriyaki chicken rice bowls", 4, "1 lb chicken breast, 2 cups white rice, 1/3 cup soy sauce, 2 tbsp honey, 2 cups broccoli, 2 cloves garlic", "Cook rice. Sauté chicken until browned, add soy sauce, honey, and garlic. Serve over rice with steamed broccoli.", ["Dinner", "Asian-Inspired", "Meal-Prep"]),
        new("Pesto chicken pasta", 4, "1 lb chicken breast, 12 oz penne pasta, 1/2 cup pesto, 1/2 cup parmesan cheese, 1 tbsp olive oil", "Cook pasta. Sauté chicken in oil until cooked through. Toss pasta and chicken with pesto and parmesan.", ["Dinner", "Italian", "Quick"]),
        new("Honey mustard chicken", 4, "1.5 lb chicken thighs, 1/4 cup honey, 1/4 cup Dijon mustard, 1 tbsp olive oil, 1 tsp garlic", "Whisk honey, mustard, oil, and garlic. Coat chicken and bake at 400 F for 25-35 minutes until cooked through.", ["Dinner", "Gluten-Free", "Meal-Prep"]),
        new("Easy chicken pot pie", 6, "2 cups cooked chicken, 2 cups frozen mixed vegetables, 1 cup milk, 1 cup chicken broth, 1 cup all-purpose flour, 1 pie crust", "Simmer chicken, vegetables, milk, broth, and flour until thick. Pour into pie dish, cover with crust, and bake at 400 F until golden.", ["Dinner", "Comfort-Food", "Family-Favorite"]),
        new("Sloppy joes", 6, "1.5 lb ground beef, 1 onion, 1/2 cup ketchup, 1 tbsp Worcestershire sauce, 6 hamburger buns", "Brown beef and onion. Stir in ketchup and Worcestershire; simmer 10 minutes. Spoon onto buns.", ["Dinner", "Budget-Friendly", "Kid-Friendly"]),
        new("Beef burrito bowls", 4, "1 lb ground beef, 2 cups white rice, 1 can black beans, 1 tomato, 1 cup shredded cheddar cheese, 1 tsp cumin", "Cook rice. Brown beef with cumin. Build bowls with rice, beef, beans, tomato, and cheese.", ["Dinner", "Mexican", "Meal-Prep"]),
        new("Easy shrimp tacos", 4, "1 lb shrimp, 8 tortillas, 1 lime, 1 tsp chili powder, 1 cup lettuce, 1 tomato", "Season shrimp with chili powder and sauté until opaque. Serve in warm tortillas with lettuce, tomato, and lime.", ["Dinner", "Mexican", "Quick", "High-Protein"]),
        new("Vegetarian chili", 6, "1 can black beans, 1 can kidney beans, 1 can diced tomatoes, 1 onion, 1 bell pepper, 2 tbsp chili powder", "Sauté onion and pepper. Add beans, tomatoes, chili powder, and 1 cup water. Simmer 25 minutes.", ["Dinner", "Vegetarian", "Vegan", "One-Pot"]),
        new("Creamy mushroom risotto", 4, "1.5 cups white rice, 8 oz mushrooms, 1 onion, 4 cups chicken broth, 1/2 cup parmesan cheese, 2 tbsp butter", "Sauté mushrooms and onion in butter. Add rice, then gradually stir in warm broth until creamy and tender. Finish with parmesan.", ["Dinner", "Comfort-Food", "Italian"]),
        new("Vegetable lasagna", 8, "12 lasagna noodles, 24 oz marinara sauce, 2 cups mozzarella cheese, 1 cup ricotta cheese, 2 cups spinach, 1 zucchini", "Layer sauce, noodles, ricotta, spinach, zucchini, and mozzarella in baking dish. Bake covered at 375 F for 35 minutes; uncover to brown.", ["Dinner", "Vegetarian", "Italian", "Freezer-Friendly"]),
        new("Baked BBQ meatloaf", 6, "1.5 lb ground beef, 1 egg, 1 cup breadcrumbs, 1/2 onion, 1/2 cup BBQ sauce", "Mix beef, egg, breadcrumbs, onion, and half the BBQ sauce. Shape into loaf, top with remaining sauce, and bake at 375 F for 50-60 minutes.", ["Dinner", "Comfort-Food", "Budget-Friendly"]),
        new("Garlic butter steak bites", 4, "1 lb steak, 3 cloves garlic, 3 tbsp butter, 1 tbsp olive oil, 1 tsp black pepper", "Cut steak into cubes. Sear in hot oil, add butter and garlic, and toss until browned to preferred doneness.", ["Dinner", "High-Protein", "Quick", "Gluten-Free"]),
        new("Lemon butter tilapia", 4, "4 tilapia fillets, 2 tbsp butter, 1 lemon, 2 cloves garlic, 1 tsp black pepper", "Place fish in baking dish. Top with melted butter, garlic, pepper, and lemon juice. Bake at 400 F for 12-15 minutes.", ["Dinner", "High-Protein", "Quick", "Gluten-Free"]),
        new("Chicken alfredo bake", 6, "2 cups cooked chicken, 12 oz penne pasta, 1 cup heavy cream, 1/2 cup parmesan cheese, 1 cup mozzarella cheese", "Cook pasta. Mix with chicken, cream, and parmesan. Top with mozzarella and bake at 375 F for 20 minutes.", ["Dinner", "Italian", "Comfort-Food"]),
        new("Salsa verde chicken", 6, "2 lb chicken breast, 2 cups salsa verde, 1 onion, 1 tsp cumin, 8 tortillas", "Place chicken, salsa verde, onion, and cumin in slow cooker. Cook until tender, shred, and serve in tortillas.", ["Dinner", "Slow-Cooker", "Mexican", "Meal-Prep"]),
        new("Crockpot beef stew", 6, "2 lb beef stew meat, 4 russet potatoes, 4 carrots, 1 onion, 4 cups beef broth, 1 tsp dried thyme", "Add all ingredients to slow cooker. Cook on low 8 hours until beef and vegetables are tender.", ["Dinner", "Slow-Cooker", "Comfort-Food", "Freezer-Friendly"]),
        new("Chicken tortilla soup", 6, "1 lb chicken breast, 1 can black beans, 1 can corn, 1 can diced tomatoes, 6 cups chicken broth, 1 tbsp chili powder", "Simmer chicken in broth with beans, corn, tomatoes, and chili powder until cooked. Shred chicken and return to soup.", ["Dinner", "Mexican", "One-Pot", "Freezer-Friendly"]),
        new("Baked chicken drumsticks", 4, "8 chicken drumsticks, 2 tbsp olive oil, 1 tsp paprika, 1 tsp garlic, 1 tsp black pepper", "Coat chicken with oil and seasonings. Bake at 425 F for 35-45 minutes until cooked through and skin is crisp.", ["Dinner", "Budget-Friendly", "Gluten-Free"]),
        new("Roasted vegetable quinoa bowls", 4, "1 cup quinoa, 1 sweet potato, 1 bell pepper, 1 broccoli, 2 tbsp olive oil, 1 lemon", "Cook quinoa. Roast chopped vegetables with olive oil at 425 F until tender. Serve over quinoa with lemon.", ["Dinner", "Vegetarian", "Healthy", "Meal-Prep"]),
        new("Chickpea coconut curry", 4, "2 cans chickpeas, 1 can diced tomatoes, 1 onion, 2 tbsp curry powder, 1 cup coconut milk, 2 cups white rice", "Sauté onion and curry powder. Add chickpeas, tomatoes, and coconut milk; simmer 20 minutes. Serve with rice.", ["Dinner", "Vegan", "Vegetarian", "One-Pot"]),
        new("Baked sweet potato fries", 4, "2 sweet potatoes, 2 tbsp olive oil, 1 tsp paprika, 1 tsp salt", "Cut sweet potatoes into fries. Toss with oil and seasonings. Bake at 425 F for 25-30 minutes, turning once.", ["Side", "Vegetarian", "Budget-Friendly"]),
        new("Garlic parmesan roasted broccoli", 4, "4 cups broccoli, 2 tbsp olive oil, 2 cloves garlic, 1/4 cup parmesan cheese, 1/2 tsp black pepper", "Toss broccoli with oil, garlic, and pepper. Roast at 425 F for 15-20 minutes. Finish with parmesan.", ["Side", "Vegetarian", "Quick", "Gluten-Free"]),
        new("Creamy mashed potatoes", 6, "2 lb russet potatoes, 1/2 cup milk, 4 tbsp butter, 1/4 cup sour cream, 1 tsp salt", "Boil peeled potatoes until tender. Drain and mash with warm milk, butter, sour cream, and salt.", ["Side", "Comfort-Food", "Family-Favorite"]),
        new("Simple garden salad", 4, "4 cups lettuce, 1 cucumber, 2 tomatoes, 1/4 red onion, 2 tbsp olive oil, 1 lemon", "Chop vegetables and combine in bowl. Dress with olive oil and lemon juice.", ["Side", "Vegetarian", "Vegan", "Quick"]),
        new("Honey glazed carrots", 4, "1 lb carrots, 2 tbsp butter, 2 tbsp honey, 1/2 tsp salt", "Steam carrots until tender. Toss in skillet with butter and honey until glossy.", ["Side", "Vegetarian", "Quick"]),
        new("Cornbread muffins", 12, "1 cup all-purpose flour, 1 cup cornmeal, 1 cup milk, 1 egg, 2 tbsp honey, 2 tsp baking powder", "Mix dry ingredients. Whisk milk, egg, and honey; combine. Divide into muffin tin and bake at 400 F for 15-18 minutes.", ["Side", "Budget-Friendly", "Kid-Friendly"]),
        new("Homemade garlic bread", 6, "1 loaf sandwich bread, 4 tbsp butter, 3 cloves garlic, 2 tbsp fresh parsley, 1/4 cup parmesan cheese", "Mix softened butter with garlic and parsley. Spread on bread, top with parmesan, and bake at 375 F until golden.", ["Side", "Italian", "Quick"]),
        new("Roasted Brussels sprouts", 4, "1 lb Brussels sprouts, 2 tbsp olive oil, 1 tsp salt, 1/2 tsp black pepper", "Trim and halve vegetables. Toss with oil, salt, and pepper. Roast at 425 F until browned and tender.", ["Side", "Vegetarian", "Vegan", "Gluten-Free"]),
        new("Classic macaroni salad", 8, "2 cups macaroni, 1/2 cup mayonnaise, 2 celery stalks, 1/4 red onion, 1 tbsp Dijon mustard", "Cook macaroni and cool. Mix with mayonnaise, celery, onion, and mustard. Chill before serving.", ["Side", "Budget-Friendly", "Meal-Prep"]),
        new("Easy coleslaw", 6, "4 cups lettuce, 1/2 cup mayonnaise, 1 tbsp apple cider vinegar, 1 tbsp honey, 1/2 tsp salt", "Whisk mayonnaise, vinegar, honey, and salt. Toss with finely shredded lettuce and chill.", ["Side", "Quick", "Budget-Friendly"]),
        new("Chocolate chip cookies", 24, "2 cups all-purpose flour, 1 cup butter, 1 cup brown sugar, 1/2 cup granulated sugar, 2 eggs, 1 cup chocolate chips", "Cream butter and sugars. Beat in eggs, fold in flour and chocolate chips. Bake at 350 F for 9-11 minutes.", ["Dessert", "Kid-Friendly", "Family-Favorite"]),
        new("Easy brownies", 16, "1 cup butter, 2 cups granulated sugar, 4 eggs, 1 cup cocoa powder, 1 cup all-purpose flour, 1 tsp vanilla extract", "Melt butter and stir in sugar. Beat in eggs and vanilla, then fold in cocoa and flour. Bake at 350 F for 25-30 minutes.", ["Dessert", "Kid-Friendly", "Comfort-Food"]),
        new("No-bake peanut butter bars", 16, "1 cup peanut butter, 1/2 cup butter, 2 cups powdered sugar, 1 cup chocolate chips", "Mix peanut butter, softened butter, and powdered sugar. Press into pan, top with melted chocolate, and chill until firm.", ["Dessert", "No-Bake", "Kid-Friendly"]),
        new("Apple crisp", 8, "6 apples, 1 cup rolled oats, 1/2 cup all-purpose flour, 1/2 cup brown sugar, 1/2 cup butter, 1 tsp cinnamon", "Slice apples into baking dish. Mix oats, flour, sugar, butter, and cinnamon; crumble over apples. Bake at 350 F for 40 minutes.", ["Dessert", "Comfort-Food", "Vegetarian"]),
        new("Banana bread", 10, "3 bananas, 2 cups all-purpose flour, 1/2 cup butter, 3/4 cup granulated sugar, 2 eggs, 1 tsp baking soda", "Mash bananas. Mix with melted butter, sugar, eggs, flour, and baking soda. Bake in loaf pan at 350 F for 55-65 minutes.", ["Dessert", "Breakfast", "Budget-Friendly"]),
        new("Strawberry yogurt parfaits", 4, "2 cups plain Greek yogurt, 1 cup strawberries, 1 cup blueberries, 1 cup rolled oats, 2 tbsp honey", "Layer yogurt, berries, and oats in glasses. Drizzle with honey and serve chilled.", ["Breakfast", "Dessert", "Vegetarian", "Quick"]),
        new("Fruit smoothie", 2, "1 banana, 1 cup strawberries, 1/2 cup blueberries, 1 cup milk, 1/2 cup plain Greek yogurt, 1 tbsp honey", "Blend all ingredients until smooth. Add a splash of milk to thin if needed.", ["Breakfast", "Snack", "Quick", "Kid-Friendly"]),
        new("Energy bites", 12, "1 cup rolled oats, 1/2 cup peanut butter, 2 tbsp honey, 1/4 cup chocolate chips", "Stir all ingredients together. Roll into bite-sized balls and chill for 30 minutes.", ["Snack", "Meal-Prep", "No-Bake"]),
        new("Crispy roasted chickpeas", 4, "1 can chickpeas, 1 tbsp olive oil, 1 tsp paprika, 1/2 tsp salt", "Drain and dry chickpeas. Toss with oil and spices. Roast at 400 F for 25-35 minutes until crisp.", ["Snack", "Vegan", "Vegetarian", "Gluten-Free"]),
        new("Homemade ranch dip", 6, "1 cup sour cream, 1/2 cup plain Greek yogurt, 1 tsp garlic, 1 tsp dried parsley, 1/2 tsp black pepper", "Stir all ingredients together. Chill for at least 20 minutes before serving with vegetables.", ["Snack", "Quick", "Vegetarian"]),
        new("Baked cinnamon apples", 4, "4 apples, 2 tbsp butter, 2 tbsp brown sugar, 1 tsp cinnamon, 1/2 cup rolled oats", "Core apples and place in baking dish. Fill with butter, brown sugar, cinnamon, and oats. Bake at 375 F until tender.", ["Dessert", "Vegetarian", "Comfort-Food"]),
        new("Lemon blueberry muffins", 12, "2 cups all-purpose flour, 1 cup blueberries, 2 eggs, 1 cup milk, 1/2 cup granulated sugar, 1 lemon, 2 tsp baking powder", "Mix flour, sugar, and baking powder. Whisk eggs, milk, and lemon zest; combine with blueberries. Bake at 375 F for 18-22 minutes.", ["Breakfast", "Dessert", "Meal-Prep"]),
        new("Vanilla rice pudding", 6, "1/2 cup white rice, 4 cups milk, 1/3 cup granulated sugar, 1 tsp vanilla extract, 1 tsp cinnamon", "Simmer rice, milk, and sugar over low heat, stirring often, until creamy and tender. Stir in vanilla and cinnamon.", ["Dessert", "Comfort-Food", "Vegetarian"]),
        new("Simple chocolate mug cake", 1, "4 tbsp all-purpose flour, 2 tbsp granulated sugar, 1 tbsp cocoa powder, 3 tbsp milk, 1 tbsp vegetable oil, 1/4 tsp baking powder", "Mix ingredients in a microwave-safe mug until smooth. Microwave 60-90 seconds until cake is set.", ["Dessert", "Quick", "Kid-Friendly"]),
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
