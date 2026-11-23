using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Location;

public sealed class SavedPlace : Entity
{
    public Guid FamilyId { get; set; }
    public Guid CreatedByMemberId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; } = 150;
    public bool AlertOnArrival { get; set; } = true;
    public bool AlertOnDeparture { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
