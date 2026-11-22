using FamilyOS.Domain.Location;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.Location;

public class GeofenceMathTests
{
    [Fact]
    public void Distance_is_zero_for_same_coordinate()
    {
        GeofenceMath.DistanceMeters(40.0, -89.0, 40.0, -89.0).Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void Distance_is_symmetric_and_positive()
    {
        var forward = GeofenceMath.DistanceMeters(40.0, -89.0, 40.001, -89.0);
        var reverse = GeofenceMath.DistanceMeters(40.001, -89.0, 40.0, -89.0);
        forward.Should().BeGreaterThan(100);
        forward.Should().BeLessThan(120);
        reverse.Should().BeApproximately(forward, 0.001);
    }

    [Fact]
    public void Entry_uses_configured_radius()
    {
        GeofenceMath.IsInside(false, 149, 150).Should().BeTrue();
        GeofenceMath.IsInside(false, 151, 150).Should().BeFalse();
    }

    [Fact]
    public void Exit_uses_hysteresis_to_prevent_boundary_chatter()
    {
        GeofenceMath.IsInside(true, 160, 150).Should().BeTrue();
        GeofenceMath.IsInside(true, 176, 150).Should().BeFalse();
    }

    [Fact]
    public void Entry_does_not_apply_exit_hysteresis()
    {
        GeofenceMath.IsInside(false, 160, 150).Should().BeFalse();
    }
}
