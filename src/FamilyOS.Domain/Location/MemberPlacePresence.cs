using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Location;

/// <summary>Deduplication state for a member's transitions across a saved-place boundary.</summary>
public sealed class MemberPlacePresence : Entity
{
    public Guid FamilyId { get; set; }
    public Guid PlaceId { get; set; }
    public Guid MemberId { get; set; }
    public bool IsInside { get; set; }
    public DateTime LastLocationUpdatedAtUtc { get; set; }
    public DateTime? LastTransitionAtUtc { get; set; }
}
