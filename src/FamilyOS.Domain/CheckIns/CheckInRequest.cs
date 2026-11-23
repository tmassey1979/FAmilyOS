using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.CheckIns;

public sealed class CheckInRequest : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid RequesterMemberId { get; private set; }
    public Guid TargetMemberId { get; private set; }
    public DateTime DueAtUtc { get; private set; }
    public string Status { get; private set; } = "Pending";
    public DateTime? RespondedAtUtc { get; private set; }
    public string? ResponseMessage { get; private set; }

    private CheckInRequest() { }

    public static CheckInRequest Create(Guid familyId, Guid requesterMemberId, Guid targetMemberId, DateTime dueAtUtc)
    {
        if (familyId == Guid.Empty || requesterMemberId == Guid.Empty || targetMemberId == Guid.Empty)
            throw new ArgumentException("Family and member IDs are required.");
        if (requesterMemberId == targetMemberId)
            throw new ArgumentException("A member cannot request a check-in from themselves.");
        if (dueAtUtc <= DateTime.UtcNow || dueAtUtc > DateTime.UtcNow.AddDays(7))
            throw new ArgumentOutOfRangeException(nameof(dueAtUtc), "Check-in due time must be within the next seven days.");
        return new CheckInRequest
        {
            FamilyId = familyId,
            RequesterMemberId = requesterMemberId,
            TargetMemberId = targetMemberId,
            DueAtUtc = DateTime.SpecifyKind(dueAtUtc, DateTimeKind.Utc)
        };
    }

    public void RespondSafe(string? message)
    {
        if (Status != "Pending") throw new InvalidOperationException("This check-in is no longer pending.");
        Status = "Safe";
        RespondedAtUtc = DateTime.UtcNow;
        ResponseMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim()[..Math.Min(message.Trim().Length, 500)];
        MarkUpdated();
    }

    public void Cancel()
    {
        if (Status != "Pending") throw new InvalidOperationException("Only pending check-ins can be cancelled.");
        Status = "Cancelled";
        MarkUpdated();
    }

    public string GetEffectiveStatus(DateTime utcNow)
        => Status == "Pending" && DueAtUtc < utcNow ? "Overdue" : Status;
}
