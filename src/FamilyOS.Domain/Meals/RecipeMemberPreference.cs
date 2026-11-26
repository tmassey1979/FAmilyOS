using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

public enum RecipePreferenceKind
{
    Like = 1,
    Dislike = 2
}

/// <summary>A family member's personal reaction and favorite flag for a recipe.</summary>
public sealed class RecipeMemberPreference : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid RecipeId { get; private set; }
    public Guid MemberId { get; private set; }
    public RecipePreferenceKind? Preference { get; private set; }
    public bool IsFavorite { get; private set; }

    private RecipeMemberPreference() { }

    public static RecipeMemberPreference Create(Guid familyId, Guid recipeId, Guid memberId,
        RecipePreferenceKind? preference, bool isFavorite)
    {
        if (familyId == Guid.Empty || recipeId == Guid.Empty || memberId == Guid.Empty)
            throw new ArgumentException("Family, recipe, and member are required.");
        return new RecipeMemberPreference
        {
            FamilyId = familyId,
            RecipeId = recipeId,
            MemberId = memberId,
            Preference = preference,
            IsFavorite = isFavorite
        };
    }

    public void Set(RecipePreferenceKind? preference, bool isFavorite)
    {
        Preference = preference;
        IsFavorite = isFavorite;
        MarkUpdated();
    }
}
