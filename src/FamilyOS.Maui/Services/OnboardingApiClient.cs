using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Maui.Networking;

namespace FamilyOS.Maui.Services;

/// <summary>Household creation and invitation acceptance using existing API contracts.</summary>
public sealed class OnboardingApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;
    private readonly StartupDiagnosticsService _diagnostics;

    public OnboardingApiClient(HttpClient http, IAuthSessionService auth, StartupDiagnosticsService diagnostics)
    {
        _http = http;
        _auth = auth;
        _diagnostics = diagnostics;
    }

    public async Task<bool> ProbeApiHealthAsync(CancellationToken ct = default)
    {
        var uri = new Uri(ApiBaseUri, "health");
        var timer = Stopwatch.StartNew();
        _diagnostics.RecordCheckpoint("AUTH API health probe started; scheme=" + uri.Scheme + "; host=" + uri.Host + "; port=" + uri.Port);
        try
        {
            using (var dns = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                dns.CancelAfter(TimeSpan.FromSeconds(8));
                var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, dns.Token);
                _diagnostics.RecordCheckpoint("AUTH API health DNS resolved; addresses=" + string.Join(",", addresses.Select(a => a.ToString())));
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            _diagnostics.RecordCheckpoint("AUTH API health response; scheme=" + uri.Scheme + "; http_status=" + (int)response.StatusCode + "; elapsed_ms=" + timer.ElapsedMilliseconds);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var http = FindException<HttpRequestException>(ex);
            var web = FindException<WebException>(ex);
            var socket = FindException<SocketException>(ex);
            _diagnostics.RecordCheckpoint("AUTH API health failed; scheme=" + uri.Scheme + "; exception_chain=" + GetExceptionTypeChain(ex) + "; http_request_error=" + (http?.HttpRequestError.ToString() ?? "none") + "; web_exception_status=" + (web?.Status.ToString() ?? "none") + "; socket_error=" + (socket?.SocketErrorCode.ToString() ?? "none") + "; elapsed_ms=" + timer.ElapsedMilliseconds);
            return false;
        }
    }

    public async Task<FamilyDto> CreateFamilyAsync(
        string name, string? displayName, string? email, string timeZone, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            name,
            timeZone,
            currency = "USD",
            ownerDisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            ownerEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim()
        }, JsonOptions);
        return await SendAsync<FamilyDto>(HttpMethod.Post, "api/family",
            new StringContent(body, Encoding.UTF8, "application/json"), authenticated: true, ct);
    }

    public Task<InvitePreviewDto> PreviewInviteAsync(string code, CancellationToken ct = default)
        => SendAsync<InvitePreviewDto>(HttpMethod.Get,
            $"api/invites/{Uri.EscapeDataString(NormalizeCode(code))}", null, authenticated: false, ct);

    public async Task<FamilyMemberDto> AcceptInviteAsync(
        string code, string? displayName, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            displayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim()
        }, JsonOptions);
        return await SendAsync<FamilyMemberDto>(HttpMethod.Post,
            $"api/invites/{Uri.EscapeDataString(NormalizeCode(code))}/accept",
            new StringContent(body, Encoding.UTF8, "application/json"), authenticated: true, ct);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content, bool authenticated, CancellationToken ct)
    {
        var requestUri = new Uri(ApiBaseUri, path);
        var elapsed = Stopwatch.StartNew();
        _diagnostics.RecordCheckpoint($"AUTH onboarding API request preparing; method={method.Method}; host={requestUri.Host}; authenticated={authenticated}");
        using var request = new HttpRequestMessage(method, requestUri) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");

        if (authenticated)
        {
            var token = await _auth.GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token))
                throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in before continuing.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _diagnostics.RecordCheckpoint("AUTH onboarding API bearer token attached; value omitted");
        }

        var network = "unavailable";
        try { network = Connectivity.Current.NetworkAccess.ToString(); }
        catch (Exception ex) { network = $"unavailable:{ex.GetType().Name}"; }
        _diagnostics.RecordCheckpoint($"AUTH onboarding API request starting; network={network}; timeout_seconds=30");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            _diagnostics.RecordCheckpoint($"AUTH onboarding API response headers received; http_status={(int)response.StatusCode}; elapsed_ms={elapsed.ElapsedMilliseconds}");
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your sign-in has expired. Sign in again.",
                HttpStatusCode.Forbidden => "This account cannot complete that onboarding action.",
                HttpStatusCode.NotFound => "That invitation could not be found.",
                HttpStatusCode.Conflict => "This account already belongs to a family.",
                _ => $"FamilyOS could not complete onboarding (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token);
            _diagnostics.RecordCheckpoint($"AUTH onboarding API response parsed; elapsed_ms={elapsed.ElapsedMilliseconds}");
            return result ?? throw new InvalidOperationException("The FamilyOS API returned an empty onboarding response.");
        }
        catch (OperationCanceledException ex)
        {
            var reason = ct.IsCancellationRequested ? "caller_cancelled" : "30_second_client_timeout";
            _diagnostics.RecordCheckpoint($"AUTH onboarding API cancelled; reason={reason}; exception={ex.GetType().Name}; elapsed_ms={elapsed.ElapsedMilliseconds}");
            throw;
        }
        catch (Exception ex)
        {
            var http = FindException<HttpRequestException>(ex);
            var web = FindException<WebException>(ex);
            var socket = FindException<SocketException>(ex);
            var webResponse = web?.Response as HttpWebResponse;
            var exceptionChain = GetExceptionTypeChain(ex);
            _diagnostics.RecordCheckpoint(
                $"AUTH onboarding API transport failed; exception_chain={exceptionChain}; " +
                $"http_request_error={http?.HttpRequestError.ToString() ?? "none"}; " +
                $"web_exception_status={web?.Status.ToString() ?? "none"}; " +
                $"web_response_status={(webResponse is null ? "none" : ((int)webResponse.StatusCode).ToString())}; " +
                $"socket_error={socket?.SocketErrorCode.ToString() ?? "none"}; " +
                $"elapsed_ms={elapsed.ElapsedMilliseconds}");
            throw;
        }
    }

    private static TException? FindException<TException>(Exception exception)
        where TException : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TException match) return match;
        }

        return null;
    }

    private static string GetExceptionTypeChain(Exception exception)
    {
        var types = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            types.Add(current.GetType().Name);
        return string.Join(">", types);
    }

    private static string NormalizeCode(string code)
        => (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
}

public sealed record InvitePreviewDto(string Code, string FamilyName, string Role, DateTime ExpiresAtUtc, bool IsOpen);
