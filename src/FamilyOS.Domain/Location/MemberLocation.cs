namespace FamilyOS.Domain.Location;

/// <summary>
/// One row per family member. Location sharing is opt-in and disabled by default.
/// Coordinates are erased when consent is revoked.
/// </summary>
public sealed class MemberLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public Guid MemberId { get; set; }
    public bool SharingEnabled { get; set; }
    public DateTime? ConsentUpdatedAtUtc { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public DateTime? LocationUpdatedAtUtc { get; set; }
}
