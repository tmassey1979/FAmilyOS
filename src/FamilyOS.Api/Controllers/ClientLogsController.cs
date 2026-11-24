using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Prometheus;

namespace FamilyOS.Api.Controllers;

/// <summary>
/// Accepts batched logs from MAUI / mobile clients so they show up in API logs and Prometheus.
/// Auth preferred; anonymous allowed with a device id for pre-login crashes.
/// </summary>
[ApiController]
[Route("api/client-logs")]
public sealed class ClientLogsController : ControllerBase
{
    private static readonly Counter ClientLogEvents = Metrics.CreateCounter(
        "familyos_client_log_events_total",
        "Client log events received by level",
        new CounterConfiguration { LabelNames = ["level", "platform"] });

    private readonly ILogger<ClientLogsController> _logger;

    public ClientLogsController(ILogger<ClientLogsController> logger) => _logger = logger;

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("client-logs")]
    public IActionResult Post([FromBody] ClientLogBatchDto? batch)
    {
        if (batch?.Entries is null || batch.Entries.Count == 0)
            return BadRequest(new { error = "entries required" });

        if (batch.Entries.Count > 100)
            return BadRequest(new { error = "max 100 entries per batch" });

        var user = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? "anonymous";
        var platform = string.IsNullOrWhiteSpace(batch.Platform) ? "unknown" : batch.Platform.Trim().ToLowerInvariant();
        var device = string.IsNullOrWhiteSpace(batch.DeviceId) ? "-" : batch.DeviceId.Trim();
        var appVersion = string.IsNullOrWhiteSpace(batch.AppVersion) ? "-" : batch.AppVersion.Trim();

        foreach (var entry in batch.Entries)
        {
            var level = NormalizeLevel(entry.Level);
            ClientLogEvents.WithLabels(level, platform).Inc();

            var msg = $"[client] {level} platform={platform} device={device} user={user} app={appVersion} " +
                      $"screen={entry.Screen ?? "-"} msg={entry.Message}";

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
