using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Prometheus;

namespace FamilyOS.Api.Controllers;

/// <summary>
/// Accepts bounded client log batches. Anonymous access supports pre-login crash reporting;
/// ingestion is rate limited and all metric labels are normalized to finite values.
/// </summary>
[ApiController]
[Route("api/client-logs")]
public sealed class ClientLogsController : ControllerBase
{
    private static readonly Counter ClientLogEvents = Metrics.CreateCounter(
        "familyos_client_log_events_total",
        "Client log events received by level",
        new CounterConfiguration { LabelNames = ["level", "platform"] });

    private static readonly HashSet<string> AllowedPlatforms =
        new(StringComparer.Ordinal) { "android", "ios", "windows", "mac-catalyst", "unknown" };

    private readonly ILogger<ClientLogsController> _logger;

    public ClientLogsController(ILogger<ClientLogsController> logger) => _logger = logger;

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("client-logs")]
    [RequestSizeLimit(64 * 1024)]
    public IActionResult Post([FromBody] ClientLogBatchDto? batch)
    {
        if (batch?.Entries is null || batch.Entries.Count == 0)
            return BadRequest(new { error = "entries required" });

        if (batch.Entries.Count > 50)
            return BadRequest(new { error = "max 50 entries per batch" });

        if (batch.Platform?.Length > 40 || batch.DeviceId?.Length > 80 || batch.AppVersion?.Length > 40)
            return BadRequest(new { error = "client metadata exceeds allowed length" });

        if (batch.Entries.Any(entry =>
                entry is null
                || string.IsNullOrWhiteSpace(entry.Message)
                || entry.Message.Length > 2000
                || entry.Screen?.Length > 120
                || entry.StackTrace?.Length > 4000))
            return BadRequest(new { error = "invalid log entry or field length exceeded" });

        var user = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? "anonymous";
        var requestedPlatform = string.IsNullOrWhiteSpace(batch.Platform)
            ? "unknown"
            : batch.Platform.Trim().ToLowerInvariant();
        var platform = AllowedPlatforms.Contains(requestedPlatform) ? requestedPlatform : "unknown";
        var device = SafeField(batch.DeviceId, 80);
        var appVersion = SafeField(batch.AppVersion, 40);

        foreach (var entry in batch.Entries)
        {
            var level = NormalizeLevel(entry.Level);
            ClientLogEvents.WithLabels(level, platform).Inc();

            var msg = $"[client] {level} platform={platform} device={device} user={user} app={appVersion} " +
                      $"screen={SafeField(entry.Screen, 120)} msg={entry.Message}";

            switch (level)
            {
                case "error":
                case "critical":
                    if (!string.IsNullOrWhiteSpace(entry.StackTrace))
                        _logger.LogError("{Message}\n{Stack}", msg, entry.StackTrace);
                    else
                        _logger.LogError("{Message}", msg);
                    break;
                case "warning":
                    _logger.LogWarning("{Message}", msg);
                    break;
                case "debug":
                    _logger.LogDebug("{Message}", msg);
                    break;
                default:
                    _logger.LogInformation("{Message}", msg);
                    break;
            }
        }

        return Accepted(new { received = batch.Entries.Count });
    }

    private static string SafeField(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        var normalized = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string NormalizeLevel(string? level)
    {
        var v = (level ?? "info").Trim().ToLowerInvariant();
        return v switch
        {
            "err" or "error" or "fatal" or "critical" => "error",
            "warn" or "warning" => "warning",
            "dbg" or "debug" or "trace" or "verbose" => "debug",
            _ => "info"
        };
    }
}

public sealed record ClientLogBatchDto(
    string? Platform,
    string? DeviceId,
    string? AppVersion,
    List<ClientLogEntryDto>? Entries);

public sealed record ClientLogEntryDto(
    string? Level,
    string? Message,
    string? Screen,
    string? StackTrace,
    DateTimeOffset? TimestampUtc);
