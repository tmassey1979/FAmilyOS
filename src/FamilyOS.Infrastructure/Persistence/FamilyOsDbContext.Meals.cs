using FamilyOS.Domain.Meals;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Infrastructure.Persistence;

public partial class FamilyOsDbContext
{
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<IngredientCatalogItem> IngredientCatalog => Set<IngredientCatalogItem>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();
    public DbSet<RecipeTagLink> RecipeTagLinks => Set<RecipeTagLink>();
    public DbSet<RecipeInstructionStep> RecipeInstructionSteps => Set<RecipeInstructionStep>();
    public DbSet<RecipeMemberPreference> RecipeMemberPreferences => Set<RecipeMemberPreference>();
    public DbSet<MealNomination> MealNominations => Set<MealNomination>();
    public DbSet<MealNominationVote> MealNominationVotes => Set<MealNominationVote>();

    private static void ConfigureMeals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MealNomination>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.WeekStartUtc });
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.PlannedSlot).HasMaxLength(20);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<MealNominationVote>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.NominationId, x.MemberId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasIndex(x => x.FamilyId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<MealPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.WeekStartUtc }).IsUnique();
            e.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.MealPlanId);
            e.Navigation(x => x.Entries).HasField("_entries");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<MealEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MealPlanId, x.DayUtc, x.Slot });
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.DishName).HasMaxLength(300).IsRequired();
            e.Property(x => x.Slot).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.IngredientsCsv).HasMaxLength(2000);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Recipe>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.Name });
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.Property(x => x.IngredientsCsv).HasMaxLength(2000);
            e.Property(x => x.Instructions).HasMaxLength(4000);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.ImageUrl).HasMaxLength(1000);
            e.Property(x => x.SourceUrl).HasMaxLength(1000);
            e.Property(x => x.Cuisine).HasMaxLength(100);
            e.Property(x => x.Difficulty).HasMaxLength(40);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<IngredientCatalogItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SearchKey);
            e.HasIndex(x => new { x.FamilyId, x.Name });
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.SearchKey).HasMaxLength(180).IsRequired();
            e.Property(x => x.Category).HasMaxLength(60).IsRequired();
            e.Property(x => x.AliasesCsv).HasMaxLength(800);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RecipeIngredient>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RecipeId, x.SortOrder });
            e.HasIndex(x => new { x.RecipeId, x.IngredientId });
            e.Property(x => x.Quantity).HasPrecision(12, 3);
            e.Property(x => x.Unit).HasMaxLength(40);
            e.Property(x => x.PreparationNote).HasMaxLength(240);
            e.Property(x => x.Section).HasMaxLength(100);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RecipeTag>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug);
            e.HasIndex(x => new { x.FamilyId, x.Slug }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RecipeTagLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RecipeId, x.TagId }).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RecipeInstructionStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RecipeId, x.StepNumber }).IsUnique();
            e.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
