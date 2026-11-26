using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Family;

public class FamilyAggregateTests
{
    [Fact]
    public void Family_Create_UpdateName()
    {
        var f = Domain.Family.Family.Create("Henderson");
        f.Name.Should().Be("Henderson");
        f.UpdateName("Henderson Family");
        f.Name.Should().Be("Henderson Family");
    }

    [Fact]
    public void FamilyMember_Create_quiet_hours_and_roles()
    {
        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var m = FamilyMember.Create(familyId, userId, FamilyRole.Teen, "Alex");

        m.DisplayName.Should().Be("Alex");
        m.Role.Should().Be(FamilyRole.Teen);
        m.UserId.Should().Be(userId);
        m.IsActive.Should().BeTrue();
        m.CanApproveRequests.Should().BeFalse();

        m.SetQuietHours(enabled: true, startMinutes: 21 * 60, endMinutes: 7 * 60);
        m.QuietHoursEnabled.Should().BeTrue();
        m.QuietHoursStartMinutes.Should().Be(21 * 60);
        m.QuietHoursEndMinutes.Should().Be(7 * 60);
        m.IsInQuietHours(TimeSpan.FromHours(22)).Should().BeTrue();

        m.ChangeRole(FamilyRole.Adult);
        m.Role.Should().Be(FamilyRole.Adult);
        m.CanApproveRequests.Should().BeTrue();
    }

    [Fact]
    public void FamilyMember_avatar_and_display_name()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Owner, "Terry");
        m.SetAvatarUrl("/avatars/x.png");
        m.AvatarUrl.Should().Be("/avatars/x.png");
        m.ClearAvatar();
        m.AvatarUrl.Should().BeNull();
        m.UpdateDisplayName("Terry M");
        m.DisplayName.Should().Be("Terry M");
    }

    [Fact]
    public void FamilyMember_deactivate()
    {
        var m = FamilyMember.Create(Guid.NewGuid(), Guid.NewGuid(), FamilyRole.Child, "Kid");
        m.Deactivate();
        m.IsActive.Should().BeFalse();
    }
}
