using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Family;

public record ProfileDto(
    Guid MemberId,
    Guid UserId,
    string DisplayName,
    FamilyRole Role,
    string? AvatarUrl,
    bool IsActive);

public record GetMyProfileQuery() : IRequest<ProfileDto>;

public record UpdateMyDisplayNameCommand(string DisplayName) : IRequest<ProfileDto>;

public record SetMyAvatarCommand(string AvatarUrl) : IRequest<ProfileDto>;

public record ClearMyAvatarCommand() : IRequest<ProfileDto>;

/// <summary>Owner (or self) can update another member's display name.</summary>
public record UpdateMemberDisplayNameCommand(Guid MemberId, string DisplayName) : IRequest<MemberDto>;

public class ProfileCommandHandlers :
    IRequestHandler<GetMyProfileQuery, ProfileDto>,
    IRequestHandler<UpdateMyDisplayNameCommand, ProfileDto>,
    IRequestHandler<SetMyAvatarCommand, ProfileDto>,
    IRequestHandler<ClearMyAvatarCommand, ProfileDto>,
    IRequestHandler<UpdateMemberDisplayNameCommand, MemberDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public ProfileCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<ProfileDto> Handle(GetMyProfileQuery query, CancellationToken ct)
    {
        var member = await LoadSelfAsync(ct);
        return ToProfile(member);
    }

    public async Task<ProfileDto> Handle(UpdateMyDisplayNameCommand cmd, CancellationToken ct)
    {
        var member = await LoadSelfAsync(ct);
        member.UpdateDisplayName(cmd.DisplayName);
        await _db.SaveChangesAsync(ct);
        return ToProfile(member);
    }

    public async Task<ProfileDto> Handle(SetMyAvatarCommand cmd, CancellationToken ct)
    {
        var member = await LoadSelfAsync(ct);
        member.SetAvatarUrl(cmd.AvatarUrl);
        await _db.SaveChangesAsync(ct);
        return ToProfile(member);
    }

    public async Task<ProfileDto> Handle(ClearMyAvatarCommand cmd, CancellationToken ct)
    {
        var member = await LoadSelfAsync(ct);
        member.ClearAvatar();
        await _db.SaveChangesAsync(ct);
        return ToProfile(member);
    }

    public async Task<MemberDto> Handle(UpdateMemberDisplayNameCommand cmd, CancellationToken ct)
    {
        var (familyId, actorMemberId, role) = await _current.RequireFamilyAsync(ct);
        var member = await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Id == cmd.MemberId && m.FamilyId == familyId, ct)
            ?? throw new NotFoundException("Member", cmd.MemberId);

        var isSelf = member.Id == actorMemberId;
        if (!isSelf && role != FamilyRole.Owner)
            throw new ForbiddenException("Only the Owner can rename other members.");

        member.UpdateDisplayName(cmd.DisplayName);
        await _db.SaveChangesAsync(ct);
        return new MemberDto(member.Id, member.UserId, member.DisplayName, member.Role, member.IsActive, member.AvatarUrl);
    }

    private async Task<FamilyMember> LoadSelfAsync(CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        return await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == familyId, ct)
            ?? throw new NotFoundException("Member", memberId);
    }

    private static ProfileDto ToProfile(FamilyMember m) =>
        new(m.Id, m.UserId, m.DisplayName, m.Role, m.AvatarUrl, m.IsActive);
}
