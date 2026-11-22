namespace FamilyOS.Domain.Location;

/// <summary>
/// Explicit permission for one active household member to view another member's latest shared location.
/// A grant never enables location sharing by itself.
/// </summary>
public sealed class MemberLocationViewerGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public Guid SharingMemberId { get; set; }
    public Guid ViewerMemberId { get; set; }
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
}
