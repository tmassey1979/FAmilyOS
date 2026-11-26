using System.Net.Http.Headers;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Services.Offline;

/// <summary>
/// Replays queued mutations when connectivity returns. Uses current access token on each attempt.
/// </summary>
public sealed class OfflineFlushService
{
    public const string ApiBaseUrl = "https://api.18.226.226.118.sslip.io/";

    private readonly OfflineStore _store;
    private readonly IAuthSessionService _auth;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public OfflineFlushService(OfflineStore store, IAuthSessionService auth, HttpClient http)
    {
        _store = store;
        _auth = auth;
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<int> FlushAsync(CancellationToken ct = default)
    {
        if (!await _flushLock.WaitAsync(0, ct).ConfigureAwait(false))
            return 0;

        try
        {
            var queue = _store.GetQueue();
            var flushed = 0;
            foreach (var item in queue.ToList())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var req = new HttpRequestMessage(new HttpMethod(item.Method), BuildUrl(item.Path));
                    req.Headers.Accept.ParseAdd("application/json");
                    var token = await _auth.GetAccessTokenAsync(ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(token))
                        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    if (!string.IsNullOrEmpty(item.Body))
                        req.Content = new StringContent(item.Body, System.Text.Encoding.UTF8, "application/json");

                    using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
                    if (res.IsSuccessStatusCode || ((int)res.StatusCode is >= 400 and < 500 and not 408 and not 429))
                    {
                        _store.RemoveFromQueue(item.Id);
                        if (res.IsSuccessStatusCode) flushed++;
                    }
                    else
                    {
                        _store.BumpAttempt(item.Id);
                        _store.MarkOffline();
                        break;
                    }
                }
                catch
                {
                    _store.BumpAttempt(item.Id);
                    _store.MarkOffline();
                    break;
                }
            }

            if (flushed > 0)
                _store.MarkOnline();
            return flushed;
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private static string BuildUrl(string path) =>
        path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"{ApiBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
