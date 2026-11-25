using FamilyOS.Application.Family;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class ProfileController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _env;

    public ProfileController(IMediator mediator, IWebHostEnvironment env)
    {
        _mediator = mediator;
        _env = env;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<ProfileDto>> GetProfile(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyProfileQuery(), ct));

    [HttpPut("profile")]
    public async Task<ActionResult<ProfileDto>> UpdateProfile([FromBody] UpdateProfileBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateMyProfileCommand(body.DisplayName), ct));

    /// <summary>Upload a JPEG/PNG/WebP profile photo (max 2 MB).</summary>
    [HttpPost("avatar")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<ProfileDto>> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Choose a photo to upload." });
        if (file.Length > 2_000_000)
            return BadRequest(new { error = "Photo must be 2 MB or smaller." });

        var contentType = file.ContentType?.ToLowerInvariant() ?? "";
        if (contentType is not ("image/jpeg" or "image/jpg" or "image/png" or "image/webp"))
            return BadRequest(new { error = "Use a JPEG, PNG, or WebP image." });

        var profile = await _mediator.Send(new GetMyProfileQuery(), ct);
        var avatarsDir = Path.Combine(_env.ContentRootPath, "avatars");
        Directory.CreateDirectory(avatarsDir);
        var ext = contentType.Contains("png") ? ".png" : contentType.Contains("webp") ? ".webp" : ".jpg";
        var fileName = $"{profile.MemberId:N}{ext}";
        var path = Path.Combine(avatarsDir, fileName);

        await using (var stream = System.IO.File.Create(path))
            await file.CopyToAsync(stream, ct);

        var publicUrl = $"/avatars/{fileName}";
        var updated = await _mediator.Send(new SetMyAvatarCommand(publicUrl), ct);
        return Ok(updated);
    }

    [HttpDelete("avatar")]
    public async Task<ActionResult<ProfileDto>> ClearAvatar(CancellationToken ct)
        => Ok(await _mediator.Send(new ClearMyAvatarCommand(), ct));

    public record UpdateProfileBody(string DisplayName);
}

[ApiController]
[Route("api/family")]
public sealed class FamilySettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FamilySettingsController(IMediator mediator) => _mediator = mediator;

    [HttpPut]
    [Authorize(Policy = FamilyAuthPolicies.OwnerOnly)]
    public async Task<ActionResult<FamilyDto>> Update([FromBody] UpdateFamilyBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateFamilyDetailsCommand(body.Name, body.TimeZone, body.Currency), ct));

    public record UpdateFamilyBody(string Name, string? TimeZone, string? Currency);
}
