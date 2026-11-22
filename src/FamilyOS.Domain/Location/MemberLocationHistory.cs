using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Location;

/// <summary>Opt-in location history point retained for at most 30 days.</summary>
public sealed class MemberLocationHistory : Entity
{
    public Guid FamilyId { get; set; }
    public Guid MemberId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AccuracyMeters { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
