using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FamilyOS.Domain.Conditions;

namespace FamilyOS.Application.Pulse;

/// <summary>
/// Family Pulse — "What matters to me right now?"
/// Deterministic priority engine, not a data dump.
/// </summary>
public record PulseDto(
    string Greeting,
    string Role,
    string? RoleHint,
    NextActionDto? NextAction,
    List<AttentionItemDto> NeedsAttention,
    List<AttentionItemDto> WaitingBlockers,
    List<UpcomingItemDto> ComingUp,
    HouseholdSummaryDto Household,
    ProcurementSummaryDto? Procurement,
    List<ConditionItemDto> Conditions);

public record NextActionDto(string Kind, Guid EntityId, string Title, string? Subtitle, string PrimaryAction, string? DueLabel);
public record AttentionItemDto(string Kind, Guid EntityId, string Title, string Reason, string ActionLabel);
public record UpcomingItemDto(string Kind, Guid EntityId, string Title, DateTime WhenUtc, string? Location);
public record HouseholdSummaryDto(int RequestsAwaitingApproval, int TasksDueToday, int TasksNeedingAcceptance, string? NextShoppingTrip);
public record ProcurementSummaryDto(int ApprovedItems, int ReadyForStore, string? PrimaryStore);
public record ConditionItemDto(Guid Id, string Name, string Status, string? RelatedTitle);

public record GetPulseQuery() : IRequest<PulseDto>;

public class PulseQueryHandler : IRequestHandler<GetPulseQuery, PulseDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public PulseQueryHandler(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PulseDto> Handle(GetPulseQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.FamilyId.HasValue || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var familyId = _current.FamilyId.Value;
        var memberId = _current.MemberId.Value;
        var role = _current.Role ?? FamilyRole.Child;

        var member = await _db.FamilyMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);
        var name = member?.DisplayName?.Split(' ').FirstOrDefault() ?? "there";
        // Greeting time-of-day is applied on the client (device local clock).
        // Server only supplies the display name so UTC hosts do not say "afternoon" at 6am local.
        var greeting = $"Welcome, {name}";

        var roleLabel = role.ToString();
        var roleHint = role switch
        {
            FamilyRole.Owner => "You see the full household — approvals, shopping, and blockers.",
            FamilyRole.Adult => "Approvals and household tasks are on your plate.",
            FamilyRole.Teen => "Your assigned tasks and any answers needed from you.",
            _ => "Your tasks and requests — keep it moving."
        };

        NextActionDto? nextAction = null;

        if (role is FamilyRole.Owner or FamilyRole.Adult)
        {
            var topPending = await _db.Requests.AsNoTracking()
                .Where(r => r.FamilyId == familyId &&
                            (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview))
                .OrderBy(r => r.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (topPending != null)
            {
                nextAction = new NextActionDto(
                    "Request", topPending.Id, topPending.Title,
                    "Needs your review", "Review", "In queue");
            }
        }

        var dueTask = await _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == familyId &&
                        ((role == FamilyRole.Owner || role == FamilyRole.Adult) || t.AssigneeMemberId == memberId) &&
                        t.DueDate != null &&
                        (t.Status == FamilyTaskStatus.Assigned ||
                         t.Status == FamilyTaskStatus.Accepted ||
                         t.Status == FamilyTaskStatus.InProgress ||
                         t.Status == FamilyTaskStatus.Paused ||
                         t.Status == FamilyTaskStatus.Deferred))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (dueTask != null)
        {
            var isAssignedToCurrentMember = dueTask.AssigneeMemberId == memberId;
            var primary = dueTask.Status switch
            {
                FamilyTaskStatus.Assigned when isAssignedToCurrentMember => "Accept",
                FamilyTaskStatus.Accepted when isAssignedToCurrentMember => "Start",
                FamilyTaskStatus.InProgress when isAssignedToCurrentMember => "Complete",
                FamilyTaskStatus.Paused when isAssignedToCurrentMember => "Resume",
                FamilyTaskStatus.Deferred when isAssignedToCurrentMember => "Start",
                _ => "Open"
            };
            nextAction = new NextActionDto(
                "Task", dueTask.Id, dueTask.Title,
                dueTask.Status.ToString(), primary,
                dueTask.DueDate.HasValue ? $"Due {dueTask.DueDate:MMM d}" : null);
        }

        var attention = new List<AttentionItemDto>();

        if (role is FamilyRole.Owner or FamilyRole.Adult)
        {
            var pending = await _db.Requests.AsNoTracking()
                .Where(r => r.FamilyId == familyId &&
                            (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview))
                .OrderBy(r => r.CreatedAtUtc)
                .Take(5)
                .ToListAsync(ct);
            foreach (var r in pending)
                attention.Add(new AttentionItemDto("Request", r.Id, r.Title, "Awaiting approval", "Review"));
        }

        var waitingInfo = await _db.Requests.AsNoTracking()
            .Where(r => r.FamilyId == familyId && r.Status == RequestStatus.WaitingForInformation &&
                        r.RequesterMemberId == memberId)
            .OrderBy(r => r.CreatedAtUtc)
            .Take(5)
            .ToListAsync(ct);
        foreach (var r in waitingInfo)
            attention.Add(new AttentionItemDto("Request", r.Id, r.Title, "Info requested", "Answer"));

        var needAccept = await _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == familyId && t.AssigneeMemberId == memberId &&
                        t.Status == FamilyTaskStatus.Assigned)
            .OrderBy(t => t.DueDate)
            .Take(5)
            .ToListAsync(ct);
        foreach (var t in needAccept)
            attention.Add(new AttentionItemDto("Task", t.Id, t.Title, "Needs acceptance", "Accept"));

        var upcoming = new List<UpcomingItemDto>();
        var window = DateTime.UtcNow.AddHours(48);
        var events = await _db.CalendarEvents.AsNoTracking()
            .Where(e => e.FamilyId == familyId && !e.IsCancelled && e.StartUtc >= DateTime.UtcNow && e.StartUtc <= window)
            .OrderBy(e => e.StartUtc)
            .Take(8)
            .ToListAsync(ct);
        foreach (var e in events)
            upcoming.Add(new UpcomingItemDto("Event", e.Id, e.Title, e.StartUtc, e.Location));

        var dueSoon = await _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == familyId &&
                        t.DueDate != null && t.DueDate > DateTime.UtcNow && t.DueDate <= window &&
                        ((role == FamilyRole.Owner || role == FamilyRole.Adult) || t.AssigneeMemberId == memberId) &&
                        t.Status != FamilyTaskStatus.Completed && t.Status != FamilyTaskStatus.Cancelled &&
                        t.Status != FamilyTaskStatus.Skipped)
            .OrderBy(t => t.DueDate)
            .Take(5)
            .ToListAsync(ct);
        foreach (var t in dueSoon)
            upcoming.Add(new UpcomingItemDto("Task", t.Id, t.Title, t.DueDate!.Value, null));

        upcoming = upcoming.OrderBy(u => u.WhenUtc).Take(10).ToList();

        var pendingApprovalCount = role is FamilyRole.Owner or FamilyRole.Adult
            ? await _db.Requests.CountAsync(r => r.FamilyId == familyId &&
                (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview), ct)
            : 0;
        var tasksDueToday = await _db.Tasks.CountAsync(t =>
            t.FamilyId == familyId && t.DueDate != null && t.DueDate.Value.Date == DateTime.UtcNow.Date &&
            ((role == FamilyRole.Owner || role == FamilyRole.Adult) || t.AssigneeMemberId == memberId) &&
            t.Status != FamilyTaskStatus.Completed && t.Status != FamilyTaskStatus.Cancelled &&
            t.Status != FamilyTaskStatus.Skipped, ct);
        var tasksNeedAccept = await _db.Tasks.CountAsync(t =>
            t.FamilyId == familyId && t.AssigneeMemberId == memberId &&
            t.Status == FamilyTaskStatus.Assigned, ct);

        var household = new HouseholdSummaryDto(pendingApprovalCount, tasksDueToday, tasksNeedAccept, null);

        ProcurementSummaryDto? procurement = null;
        if (role is FamilyRole.Owner or FamilyRole.Adult)
        {
            var approved = await _db.ProcurementItems.CountAsync(p =>
                p.FamilyId == familyId && p.Status == ProcurementItemStatus.Approved, ct);
            var inCart = await _db.ProcurementItems.CountAsync(p =>
                p.FamilyId == familyId && p.Status == ProcurementItemStatus.InCart, ct);

            if (approved > 0 || inCart > 0)
                procurement = new ProcurementSummaryDto(approved + inCart, inCart, "Walmart");
        }

        var conditions = await _db.Conditions.AsNoTracking()
            .Where(c => c.FamilyId == familyId && c.Status == ConditionStatus.Pending)
            .OrderBy(c => c.DeadlineUtc)
            .Take(5)
            .ToListAsync(ct);

        var conditionItems = conditions.Select(c => new ConditionItemDto(
            c.Id, c.Name, c.Status.ToString(), c.Description)).ToList();

        var waitingBlockers = new List<AttentionItemDto>();
        foreach (var r in waitingInfo)
            waitingBlockers.Add(new AttentionItemDto("Request", r.Id, r.Title, "Waiting for your answer", "Answer"));

        foreach (var c in conditions)
            waitingBlockers.Add(new AttentionItemDto(
                "Condition", c.Id, c.Name,
                c.DeadlineUtc.HasValue ? $"Pending · due {c.DeadlineUtc:MMM d}" : "Pending condition",
                "Satisfy"));

        var overdueMine = await _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == familyId && t.AssigneeMemberId == memberId &&
                        t.DueDate != null && t.DueDate < DateTime.UtcNow &&
                        t.Status != FamilyTaskStatus.Completed && t.Status != FamilyTaskStatus.Cancelled &&
                        t.Status != FamilyTaskStatus.Skipped)
            .OrderBy(t => t.DueDate)
            .Take(5)
            .ToListAsync(ct);
        foreach (var t in overdueMine)
            waitingBlockers.Add(new AttentionItemDto("Task", t.Id, t.Title, "Overdue", "Open"));

        var conditional = await _db.Requests.AsNoTracking()
            .Where(r => r.FamilyId == familyId && r.Status == RequestStatus.ConditionallyApproved)
            .OrderBy(r => r.CreatedAtUtc)
            .Take(5)
            .ToListAsync(ct);
        foreach (var r in conditional)
            waitingBlockers.Add(new AttentionItemDto(
                "Request", r.Id, r.Title, "Blocked on conditions", "Review"));

        return new PulseDto(
            greeting, roleLabel, roleHint,
            nextAction, attention, waitingBlockers, upcoming, household, procurement, conditionItems);
    }
}
