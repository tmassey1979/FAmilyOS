using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>
/// Buffers client logs and posts them to POST /api/client-logs for server-side observability.
/// </summary>
public sealed class ClientLogService : IDisposable
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;
    private readonly ConcurrentQueue<ClientLogEntry> _queue = new();
    private readonly Timer _timer;
    private readonly string _deviceId;
    private readonly string _platform;
    private readonly string _appVersion;
    private int _flushing;

    public ClientLogService(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
        _deviceId = Preferences.Default.Get("familyos.device-id", string.Empty);
        if (string.IsNullOrEmpty(_deviceId))
        {
            _deviceId = Guid.NewGuid().ToString("N");
            Preferences.Default.Set("familyos.device-id", _deviceId);
        }

        _platform = DeviceInfo.Platform.ToString();
        _appVersion = AppInfo.VersionString;
        _timer = new Timer(async _ => await FlushAsync(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(20));
    }

    public void Info(string message, string? screen = null) => Enqueue("info", message, screen);
    public void Warn(string message, string? screen = null) => Enqueue("warning", message, screen);
    public void Error(string message, Exception? ex = null, string? screen = null)
        => Enqueue("error", message, screen, ex?.ToString());

    private void Enqueue(string level, string message, string? screen, string? stack = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (_queue.Count > 200) _queue.TryDequeue(out _);
        _queue.Enqueue(new ClientLogEntry(
            level,
            message.Length > 2000 ? message[..2000] : message,
            screen,
            stack is { Length: > 4000 } ? stack[..4000] : stack,
            DateTimeOffset.UtcNow));
    }

    public async Task FlushAsync()
    {
        if (Interlocked.Exchange(ref _flushing, 1) == 1) return;
        try
        {
            var batch = new List<ClientLogEntry>(32);
            while (batch.Count < 50 && _queue.TryDequeue(out var item))
                batch.Add(item);
            if (batch.Count == 0) return;

            var payload = new
            {
                platform = _platform,
                deviceId = _deviceId,
                appVersion = _appVersion,
                entries = batch.Select(e => new
                {
                    level = e.Level,
                    message = e.Message,
                    screen = e.Screen,
                    stackTrace = e.StackTrace,
                    timestampUtc = e.TimestampUtc
                })
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ApiBaseUri, "api/client-logs"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            try
            {
                var token = await _auth.GetAccessTokenAsync();
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            catch { /* still send anonymously */ }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            using var response = await _http.SendAsync(request, cts.Token);
            // Best-effort: drop on failure to avoid infinite retry loops offline.
            if (!response.IsSuccessStatusCode && batch.Count < 20)
            {
                foreach (var e in batch)
                    _queue.Enqueue(e);
            }
        }
        catch
        {
            // Offline / API down — keep buffer for next tick when possible.
        }
        finally
        {
            Interlocked.Exchange(ref _flushing, 0);
        }
    }

    public void Dispose() => _timer.Dispose();

    private sealed record ClientLogEntry(string Level, string Message, string? Screen, string? StackTrace, DateTimeOffset TimestampUtc);
}
