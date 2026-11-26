using FamilyOS.Domain.Common;
using FamilyOS.Domain.Meals;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Meals;

public class MealPlanTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Theory]
    [InlineData(2026, 10, 14)] // Wed
    [InlineData(2026, 10, 12)] // Mon
    [InlineData(2026, 10, 11)] // Sun
    [InlineData(2026, 10, 17)] // Sat
    public void ToMonday_always_returns_monday(int y, int m, int d)
    {
        var any = new DateTime(y, m, d, 15, 30, 0, DateTimeKind.Utc);
        var monday = MealPlan.ToMonday(any);
        monday.DayOfWeek.Should().Be(DayOfWeek.Monday);
        monday.Kind.Should().Be(DateTimeKind.Utc);
        monday.TimeOfDay.Should().Be(TimeSpan.Zero);
        // Monday is never after the input date
        monday.Date.Should().BeOnOrBefore(any.Date);
        // At most 6 days earlier
        (any.Date - monday.Date).TotalDays.Should().BeInRange(0, 6);
    }

    [Fact]
    public void Create_sets_week_start_to_monday()
    {
        var plan = MealPlan.Create(FamilyId, new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc));
        plan.FamilyId.Should().Be(FamilyId);
        plan.WeekStartUtc.DayOfWeek.Should().Be(DayOfWeek.Monday);
        plan.Entries.Should().BeEmpty();
    }

    [Fact]
    public void AddEntry_allows_multiple_dishes_for_the_same_meal()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var day = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        var main = plan.AddEntry(day, MealSlot.Dinner, "Beef stroganoff", notes: "Main dish");
        var side = plan.AddEntry(day, MealSlot.Dinner, "Roasted broccoli");
        var bread = plan.AddEntry(day, MealSlot.Dinner, "Garlic bread");

        plan.Entries.Should().HaveCount(3);
        plan.Entries.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        plan.Entries.Select(x => x.DishName).Should().Contain(new[] { "Beef stroganoff", "Roasted broccoli", "Garlic bread" });
        main.Slot.Should().Be(MealSlot.Dinner);
        side.DayUtc.Date.Should().Be(day.Date);

        var updated = plan.UpdateEntry(side.Id, "Roasted asparagus");
        plan.Entries.Should().HaveCount(3);
        updated.Id.Should().Be(side.Id);
        updated.DishName.Should().Be("Roasted asparagus");
        bread.DishName.Should().Be("Garlic bread");
    }

    [Fact]
    public void RemoveEntry_soft_deletes()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var e = plan.AddEntry(DateTime.UtcNow.Date, MealSlot.Breakfast, "Eggs");
        plan.RemoveEntry(e.Id);
        e.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void AddEntry_rejects_empty_dish()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var act = () => plan.AddEntry(DateTime.UtcNow.Date, MealSlot.Lunch, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllIngredients_skips_deleted()
    {
        var plan = MealPlan.Create(FamilyId, DateTime.UtcNow);
        var day = DateTime.UtcNow.Date;
        var e = plan.AddEntry(day, MealSlot.Dinner, "Pasta", ingredientsCsv: "pasta, garlic");
        plan.AddEntry(day, MealSlot.Lunch, "Salad", ingredientsCsv: "lettuce");
        plan.RemoveEntry(e.Id);
        plan.AllIngredients().Should().Equal("lettuce");
    }
}
