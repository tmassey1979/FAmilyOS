using FamilyOS.Domain.CheckIns;
using FluentAssertions;

namespace FamilyOS.Domain.Tests.CheckIns;

public class CheckInRequestTests
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    [Fact]
    public void Create_starts_pending_and_preserves_participants()
    {
        var due = DateTime.UtcNow.AddHours(2);

        var request = CheckInRequest.Create(FamilyId, RequesterId, TargetId, due);

        request.FamilyId.Should().Be(FamilyId);
        request.RequesterMemberId.Should().Be(RequesterId);
        request.TargetMemberId.Should().Be(TargetId);
        request.Status.Should().Be("Pending");
        request.DueAtUtc.Should().BeCloseTo(due, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_rejects_missing_ids()
    {
        var act = () => CheckInRequest.Create(Guid.Empty, RequesterId, TargetId, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_self_check_in()
    {
        var act = () => CheckInRequest.Create(FamilyId, RequesterId, RequesterId, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_due_time_outside_seven_day_window()
    {
        var tooSoon = () => CheckInRequest.Create(FamilyId, RequesterId, TargetId, DateTime.UtcNow);
        var tooLate = () => CheckInRequest.Create(FamilyId, RequesterId, TargetId, DateTime.UtcNow.AddDays(8));

        tooSoon.Should().Throw<ArgumentOutOfRangeException>();
        tooLate.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RespondSafe_marks_request_safe_and_trims_message()
    {
        var request = CreateRequest();

        request.RespondSafe("  I'm okay.  ");

        request.Status.Should().Be("Safe");
        request.RespondedAtUtc.Should().NotBeNull();
        request.ResponseMessage.Should().Be("I'm okay.");
    }

    [Fact]
    public void RespondSafe_limits_message_to_500_characters()
    {
        var request = CreateRequest();

        request.RespondSafe(new string('x', 600));

        request.ResponseMessage.Should().HaveLength(500);
    }

    [Fact]
    public void RespondSafe_cannot_be_repeated()
    {
        var request = CreateRequest();
        request.RespondSafe(null);

        var act = () => request.RespondSafe(null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_changes_pending_request_to_cancelled()
    {
        var request = CreateRequest();

        request.Cancel();

        request.Status.Should().Be("Cancelled");
    }

    [Fact]
    public void Cancel_cannot_cancel_completed_request()
    {
        var request = CreateRequest();
        request.RespondSafe(null);

        var act = () => request.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Effective_status_becomes_overdue_without_blocking_safe_response()
    {
        var request = CreateRequest(DateTime.UtcNow.AddMinutes(-1));

        request.GetEffectiveStatus(DateTime.UtcNow).Should().Be("Overdue");
        request.Status.Should().Be("Pending");

        request.RespondSafe(null);

        request.Status.Should().Be("Safe");
    }

    private static CheckInRequest CreateRequest(DateTime? dueAtUtc = null)
        => CheckInRequest.Create(FamilyId, RequesterId, TargetId, dueAtUtc ?? DateTime.UtcNow.AddHours(1));
}
