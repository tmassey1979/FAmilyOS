using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Family;

public record ProfileDto(Guid MemberId, string DisplayName, string Role, string? AvatarUrl);

public record GetMyProfileQuery() : IRequest<ProfileDto>;
public record UpdateMyProfileCommand(string DisplayName) : IRequest<ProfileDto>;
public record SetMyAvatarCommand(string AvatarUrl) : IRequest<ProfileDto>;
public record ClearMyAvatarCommand() : IRequest<ProfileDto>;
public record UpdateFamilyDetailsCommand(string Name, string? TimeZone, string? Currency) : IRequest<FamilyDto>;

public class ProfileCommandHandlers :
    IRequestHandler<GetMyProfileQuery, ProfileDto>,
    IRequestHandler<UpdateMyProfileCommand, ProfileDto>,
    IRequestHandler<SetMyAvatarCommand, ProfileDto>,
    IRequestHandler<ClearMyAvatarCommand, ProfileDto>,
    IRequestHandler<UpdateFamilyDetailsCommand, FamilyDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public ProfileCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<ProfileDto> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var (familyId, memberId, role) = await _current.RequireFamilyAsync(ct);
        var member = await _db.FamilyMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == familyId, ct)
            ?? throw new NotFoundException("Member", memberId);
        return new ProfileDto(member.Id, member.DisplayName, member.Role.ToString(), member.AvatarUrl);
    }

    public async Task<ProfileDto> Handle(UpdateMyProfileCommand request, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var member = await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == familyId, ct)
            ?? throw new NotFoundException("Member", memberId);
        member.UpdateDisplayName(request.DisplayName);
        await _db.SaveChangesAsync(ct);
        return new ProfileDto(member.Id, member.DisplayName, member.Role.ToString(), member.AvatarUrl);
    }

    public async Task<ProfileDto> Handle(SetMyAvatarCommand request, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var member = await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == familyId, ct)
            ?? throw new NotFoundException("Member", memberId);
        member.SetAvatarUrl(request.AvatarUrl);
        await _db.SaveChangesAsync(ct);
        return new ProfileDto(member.Id, member.DisplayName, member.Role.ToString(), member.AvatarUrl);
    }

    public async Task<ProfileDto> Handle(ClearMyAvatarCommand request, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var member = await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == familyId, ct)
            ?? throw new NotFoundException("Member", memberId);
        member.SetAvatarUrl(null);
        await _db.SaveChangesAsync(ct);
        return new ProfileDto(member.Id, member.DisplayName, member.Role.ToString(), member.AvatarUrl);
    }

    public async Task<FamilyDto> Handle(UpdateFamilyDetailsCommand request, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role != FamilyRole.Owner)
            throw new ForbiddenException("Only the Owner can update household settings.");

        var family = await _db.Families
            .Include(f => f.Members)
            .FirstOrDefaultAsync(f => f.Id == familyId, ct)
            ?? throw new NotFoundException("Family", familyId);

        family.UpdateDetails(request.Name, request.TimeZone, request.Currency);
        await _db.SaveChangesAsync(ct);

        var currentMemberId = _current.MemberId
            ?? throw new ForbiddenException("Not a family member.");
        return new FamilyDto(
            family.Id, family.Name, family.TimeZone, family.Currency,
            family.Members.Where(m => m.IsActive)
                .Select(m => new MemberDto(m.Id, m.UserId, m.DisplayName, m.Role, m.IsActive, m.AvatarUrl))
                .ToList(),
            currentMemberId);
    }
}
