using FamilyOS.Application.Family;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class ProfileController : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IMediator mediator, IWebHostEnvironment env, ILogger<ProfileController> logger)
    {
        _mediator = mediator;
        _env = env;
        _logger = logger;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<ProfileDto>> GetProfile(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyProfileQuery(), ct));

    [HttpPut("profile")]
    public async Task<ActionResult<ProfileDto>> UpdateProfile([FromBody] UpdateProfileBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.DisplayName))
            return BadRequest(new { detail = "Display name is required." });
        var dto = await _mediator.Send(new UpdateMyDisplayNameCommand(body.DisplayName.Trim()), ct);
        return Ok(dto);
    }

    /// <summary>Upload a profile photo (multipart field name: file). Max ~20 MB. JPEG/PNG/WebP/GIF.</summary>
    [HttpPost("avatar")]
    [RequestSizeLimit(22_000_000)]
    public async Task<ActionResult<ProfileDto>> UploadAvatar(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "No image file provided." });
        if (file.Length > 20_000_000)
            return BadRequest(new { detail = "Original image must be 20 MB or smaller." });

        var contentType = (file.ContentType ?? "").Split(';', 2)[0].Trim().ToLowerInvariant();
        if (!AllowedContentTypes.Contains(contentType))
            return BadRequest(new { detail = "Only JPEG, PNG, WebP, or GIF images are allowed." });

        var ext = contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        var avatarsDir = Path.Combine(_env.ContentRootPath, "avatars");
        Directory.CreateDirectory(avatarsDir);

        // Prefer member id in filename for easy cleanup / overwrite
        var current = await _mediator.Send(new GetMyProfileQuery(), ct);
        var fileName = $"{current.MemberId:N}{ext}";
        var path = Path.Combine(avatarsDir, fileName);

        // Remove prior extensions for same member
        foreach (var old in Directory.EnumerateFiles(avatarsDir, $"{current.MemberId:N}.*"))
        {
            try { System.IO.File.Delete(old); } catch { /* ignore */ }
        }

        await using (var stream = System.IO.File.Create(path))
            await file.CopyToAsync(stream, ct);

        var relativeUrl = $"/avatars/{fileName}";
        var dto = await _mediator.Send(new SetMyAvatarCommand(relativeUrl), ct);
        _logger.LogInformation("Avatar uploaded for member {MemberId}", current.MemberId);
        return Ok(dto);
    }

    [HttpDelete("avatar")]
    public async Task<ActionResult<ProfileDto>> ClearAvatar(CancellationToken ct)
    {
        var current = await _mediator.Send(new GetMyProfileQuery(), ct);
        if (!string.IsNullOrWhiteSpace(current.AvatarUrl))
        {
            var name = Path.GetFileName(current.AvatarUrl);
            if (!string.IsNullOrWhiteSpace(name))
            {
                var path = Path.Combine(_env.ContentRootPath, "avatars", name);
                try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { /* ignore */ }
            }
        }

        var dto = await _mediator.Send(new ClearMyAvatarCommand(), ct);
        return Ok(dto);
    }
}

public sealed class UpdateProfileBody
{
    public string DisplayName { get; set; } = "";
}
