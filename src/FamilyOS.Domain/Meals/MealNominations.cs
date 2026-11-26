using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

public enum MealNominationStatus
{
    Open = 0,
    Selected = 1,
    Declined = 2
}

public class MealNomination : Entity
{
    public Guid FamilyId { get; private set; }
    public DateTime WeekStartUtc { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid? RecipeId { get; private set; }
    public Guid ProposedByMemberId { get; private set; }
    public string? Note { get; private set; }
    public MealNominationStatus Status { get; private set; }
    public Guid? DecidedByMemberId { get; private set; }
    public DateTime? DecisionAtUtc { get; private set; }
    public DateTime? PlannedDayUtc { get; private set; }
    public string? PlannedSlot { get; private set; }

    private MealNomination() { }

    public static MealNomination Create(Guid familyId, DateTime weekStartUtc, string title,
        Guid proposedByMemberId, Guid? recipeId = null, string? note = null)
    {
        if (familyId == Guid.Empty || proposedByMemberId == Guid.Empty)
            throw new ArgumentException("Family and proposing member are required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A meal name is required.", nameof(title));
        return new MealNomination
        {
            FamilyId = familyId,
            WeekStartUtc = MealPlan.ToMonday(weekStartUtc),
            Title = title.Trim(),
            ProposedByMemberId = proposedByMemberId,
            RecipeId = recipeId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Status = MealNominationStatus.Open
        };
    }

    public void Decide(Guid parentMemberId, bool selected, DateTime? plannedDayUtc = null, string? plannedSlot = null)
    {
        if (parentMemberId == Guid.Empty) throw new ArgumentException("A parent decision maker is required.");
        if (selected && (plannedDayUtc is null || !Enum.TryParse<MealSlot>(plannedSlot, true, out _)))
            throw new ArgumentException("A selected meal needs a planned day and valid meal slot.");
        Status = selected ? MealNominationStatus.Selected : MealNominationStatus.Declined;
        DecidedByMemberId = parentMemberId;
        DecisionAtUtc = DateTime.UtcNow;
        PlannedDayUtc = selected ? DateTime.SpecifyKind(plannedDayUtc!.Value.Date, DateTimeKind.Utc) : null;
        PlannedSlot = selected ? plannedSlot!.Trim() : null;
        MarkUpdated();
    }
}

public class MealNominationVote : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid NominationId { get; private set; }
    public Guid MemberId { get; private set; }
    private MealNominationVote() { }

    public static MealNominationVote Create(Guid familyId, Guid nominationId, Guid memberId)
    {
        if (familyId == Guid.Empty || nominationId == Guid.Empty || memberId == Guid.Empty)
            throw new ArgumentException("Family, nomination, and member are required.");
        return new MealNominationVote { FamilyId = familyId, NominationId = nominationId, MemberId = memberId };
    }
}
