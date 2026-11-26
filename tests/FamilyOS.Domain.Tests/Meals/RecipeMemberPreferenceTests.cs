using FamilyOS.Domain.Meals;
using Xunit;

namespace FamilyOS.Domain.Tests.Meals;

public sealed class RecipeMemberPreferenceTests
{
    [Fact]
    public void Member_can_like_and_favorite_a_recipe()
    {
        var preference = RecipeMemberPreference.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RecipePreferenceKind.Like, true);

        Assert.Equal(RecipePreferenceKind.Like, preference.Preference);
        Assert.True(preference.IsFavorite);
    }

    [Fact]
    public void Member_can_change_feedback_without_losing_favorite()
    {
        var preference = RecipeMemberPreference.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RecipePreferenceKind.Like, true);

        preference.Set(RecipePreferenceKind.Dislike, true);

        Assert.Equal(RecipePreferenceKind.Dislike, preference.Preference);
        Assert.True(preference.IsFavorite);
    }

    [Fact]
    public void Member_can_clear_feedback_and_favorite()
    {
        var preference = RecipeMemberPreference.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RecipePreferenceKind.Like, true);

        preference.Set(null, false);

        Assert.Null(preference.Preference);
        Assert.False(preference.IsFavorite);
    }

    [Fact]
    public void Create_rejects_missing_family_recipe_or_member()
    {
        Assert.Throws<ArgumentException>(() => RecipeMemberPreference.Create(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), RecipePreferenceKind.Like, false));
        Assert.Throws<ArgumentException>(() => RecipeMemberPreference.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), RecipePreferenceKind.Like, false));
        Assert.Throws<ArgumentException>(() => RecipeMemberPreference.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, RecipePreferenceKind.Like, false));
    }
}
