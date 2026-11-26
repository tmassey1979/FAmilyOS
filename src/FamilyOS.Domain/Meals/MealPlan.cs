using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

public class MealPlan : Entity
{
    public Guid FamilyId { get; private set; }
    public DateTime WeekStartUtc { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<MealEntry> _entries = new();
    public IReadOnlyCollection<MealEntry> Entries => _entries.AsReadOnly();

    private MealPlan() { }

    /// <summary>Normalize any date to the Monday 00:00 UTC of that ISO-style week (Mon–Sun).</summary>
    public static DateTime ToMonday(DateTime anyUtc)
    {
        var d = DateTime.SpecifyKind(anyUtc.Date, DateTimeKind.Utc);
        // DayOfWeek: Sunday=0 … Saturday=6. Convert so Monday=0 … Sunday=6.
        var daysFromMonday = d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1;
        return d.AddDays(-daysFromMonday);
    }

    public static MealPlan Create(Guid familyId, DateTime weekStartUtc, string? notes = null)
    {
        return new MealPlan
        {
            FamilyId = familyId,
            WeekStartUtc = ToMonday(weekStartUtc),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public MealEntry AddEntry(
        DateTime dayUtc,
        MealSlot slot,
        string dishName,
        string? notes = null,
        string? ingredientsCsv = null,
        Guid? recipeId = null)
    {
        if (string.IsNullOrWhiteSpace(dishName))
            throw new ArgumentException("Dish name is required.", nameof(dishName));

        var entry = MealEntry.Create(Id, FamilyId, dayUtc.Date, slot, dishName.Trim(), notes, ingredientsCsv, recipeId);
        _entries.Add(entry);
        MarkUpdated();
        return entry;
    }

    public MealEntry UpdateEntry(
        Guid entryId,
        string dishName,
        string? notes = null,
        string? ingredientsCsv = null,
        Guid? recipeId = null)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == entryId && !e.IsDeleted)
            ?? throw new InvalidOperationException("The dish no longer exists in this meal plan.");
        entry.Update(dishName, notes, ingredientsCsv, recipeId);
        MarkUpdated();
        return entry;
    }

    public void RemoveEntry(Guid entryId)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == entryId);
        if (entry is null) return;
        entry.SoftDelete();
        MarkUpdated();
    }

    public void SetNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        MarkUpdated();
    }

    public IReadOnlyList<string> AllIngredients()
    {
        return _entries
            .Where(e => !e.IsDeleted)
            .SelectMany(e => e.IngredientList())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }
}
