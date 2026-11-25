using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Family;

public record FamilyDto(Guid Id, string Name, string? TimeZone, string? Currency, List<MemberDto> Members, Guid CurrentMemberId);
public record MemberDto(Guid Id, Guid UserId, string DisplayName, FamilyRole Role, bool IsActive, string? AvatarUrl = null);
public record DeclineReasonDto(Guid Id, string Text, int SortOrder, bool IsEnabled);

public record GetMyFamilyQuery() : IRequest<FamilyDto>;
public record GetDeclineReasonsQuery() : IRequest<List<DeclineReasonDto>>;
public record GetMembersQuery() : IRequest<List<MemberDto>>;

public class FamilyQueryHandlers :
    IRequestHandler<GetMyFamilyQuery, FamilyDto>,
    IRequestHandler<GetDeclineReasonsQuery, List<DeclineReasonDto>>,
    IRequestHandler<GetMembersQuery, List<MemberDto>>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public FamilyQueryHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<FamilyDto> Handle(GetMyFamilyQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.FamilyId.HasValue)
            throw new ForbiddenException("Not a family member.");

        var family = await _db.Families
            .Include(f => f.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == _current.FamilyId, ct)
            ?? throw new NotFoundException("Family", _current.FamilyId);

        return new FamilyDto(
            family.Id, family.Name, family.TimeZone, family.Currency,
            family.Members.Where(m => m.IsActive).Select(m =>
                new MemberDto(m.Id, m.UserId, m.DisplayName, m.Role, m.IsActive, m.AvatarUrl)).ToList(),
            _current.MemberId ?? throw new ForbiddenException("Not a family member."));
    }

    public async Task<List<DeclineReasonDto>> Handle(GetDeclineReasonsQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.FamilyId.HasValue)
            throw new ForbiddenException("Not a family member.");

        return await _db.DeclineReasons.AsNoTracking()
            .Where(d => d.FamilyId == _current.FamilyId && d.IsEnabled)
            .OrderBy(d => d.SortOrder)
            .Select(d => new DeclineReasonDto(d.Id, d.Text, d.SortOrder, d.IsEnabled))
            .ToListAsync(ct);
    }

    public async Task<List<MemberDto>> Handle(GetMembersQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.FamilyId.HasValue)
            throw new ForbiddenException("Not a family member.");

        return await _db.FamilyMembers.AsNoTracking()
            .Where(m => m.FamilyId == _current.FamilyId && m.IsActive)
            .Select(m => new MemberDto(m.Id, m.UserId, m.DisplayName, m.Role, m.IsActive, m.AvatarUrl))
            .ToListAsync(ct);
    }
}
