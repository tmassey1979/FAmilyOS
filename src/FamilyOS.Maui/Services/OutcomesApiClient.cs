using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Reads household outcome metrics from the authenticated API.</summary>
public sealed class OutcomesApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public OutcomesApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<OutcomesDto> GetAsync(CancellationToken ct = default)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to view household outcomes.");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiBaseUri, "api/outcomes"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session has expired. Sign in again.",
                HttpStatusCode.Forbidden => "Your account cannot view this household's outcomes.",
                _ => $"FamilyOS could not load outcomes (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<OutcomesDto>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("The FamilyOS outcomes API returned an empty response.");
    }
}

public sealed record OutcomesDto(
    int TasksCompletedLast7Days,
    int TasksCompletedLast30Days,
    int RequestsApprovedLast7Days,
    int RequestsDeniedLast7Days,
    int RequestsApprovedLast30Days,
    int RequestsDeniedLast30Days,
    int ShoppingItemsPurchasedLast7Days,
    double? AvgTaskCompletionMinutes,
    double? ApprovalRateLast7Days,
    int OpenTasks,
    int PendingRequests,
    int QueuedShoppingItems,
    IReadOnlyList<int> WeeklyTasksCompleted,
    IReadOnlyList<int> WeeklyRequestsApproved,
    IReadOnlyList<int> WeeklyShoppingPurchased);
