using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Authenticated access to the existing request workflow endpoints.</summary>
public sealed class RequestsApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public RequestsApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public Task<List<FamilyRequestDto>> GetMineAsync(CancellationToken ct = default)
        => SendAsync<List<FamilyRequestDto>>(HttpMethod.Get, "api/requests/mine", null, ct);

    public Task<List<FamilyRequestDto>> GetApprovalQueueAsync(CancellationToken ct = default)
        => SendAsync<List<FamilyRequestDto>>(HttpMethod.Get, "api/requests/approval-queue", null, ct);

    public Task<List<RequestTypeOptionDto>> GetTypesAsync(CancellationToken ct = default)
        => SendAsync<List<RequestTypeOptionDto>>(HttpMethod.Get, "api/requests/types", null, ct);

    public Task<FamilyRequestDto> GetAsync(Guid id, CancellationToken ct = default)
        => SendAsync<FamilyRequestDto>(HttpMethod.Get, $"api/requests/{id:D}", null, ct);

    public Task<ExecutionPlanDto> GenerateExecutionPlanAsync(Guid requestId, CancellationToken ct = default)
        => SendAsync<ExecutionPlanDto>(HttpMethod.Post, $"api/requests/{requestId:D}/execution-plan", null, ct);

    public Task<ExecutionPlanDto> GetExecutionPlanAsync(Guid planId, CancellationToken ct = default)
        => SendAsync<ExecutionPlanDto>(HttpMethod.Get, $"api/execution-plans/{planId:D}", null, ct);

    public Task<ExecutionPlanDto> CommitExecutionPlanAsync(Guid planId, List<Guid> deselectedItemIds, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { deselectedItemIds }, JsonOptions);
        return SendAsync<ExecutionPlanDto>(HttpMethod.Post, $"api/requests/execution-plans/{planId:D}/commit",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
    }

    public Task<FamilyRequestDto> CreateAsync(string type, string title, Dictionary<string, object?> answers,
        decimal? amount = null, DateTime? neededBy = null, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { type, title, answers, amount, neededBy }, JsonOptions);
        return SendAsync<FamilyRequestDto>(HttpMethod.Post, "api/requests",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
    }

    public Task<FamilyRequestDto> UpdateDraftAsync(Guid id, string title, Dictionary<string, object?> answers,
        decimal? amount = null, DateTime? neededBy = null, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { title, answers, amount, neededBy }, JsonOptions);
        return SendAsync<FamilyRequestDto>(HttpMethod.Put, $"api/requests/{id:D}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
    }

    public Task<FamilyRequestDto> AnswerQuestionAsync(Guid id, Guid questionId, string answer, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { questionId, answer }, JsonOptions);
        return SendAsync<FamilyRequestDto>(HttpMethod.Post, $"api/requests/{id:D}/answer",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
    }

    public Task<FamilyRequestDto> RunActionAsync(Guid id, string action, string? reason = null, CancellationToken ct = default)
    {
        var body = action switch
        {
            "approve" => JsonSerializer.Serialize(new { conditional = false }, JsonOptions),
            "deny" => JsonSerializer.Serialize(new { reason = string.IsNullOrWhiteSpace(reason) ? "Not approved" : reason }, JsonOptions),
            "cancel" => JsonSerializer.Serialize(new { reason }, JsonOptions),
            _ => "{}"
        };
        var allowed = new[] { "submit", "approve", "deny", "cancel" };
        if (!allowed.Contains(action, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(action), "Unsupported request action.");
        var content = action == "submit" ? null : new StringContent(body, Encoding.UTF8, "application/json");
        return SendAsync<FamilyRequestDto>(HttpMethod.Post, $"api/requests/{id:D}/{action}", content, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to view family requests.");

        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
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
                HttpStatusCode.Forbidden => "Your family role does not allow that request action.",
                HttpStatusCode.NotFound => "That request could not be found.",
                _ => $"FamilyOS could not complete the request action (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token);
        return result ?? throw new InvalidOperationException("The FamilyOS API returned an empty request response.");
    }
}

public sealed record FamilyRequestDto(
    Guid Id, string Type, string Status, string Title, string? Summary,
    decimal? Amount, DateTime? NeededByUtc, Guid RequesterMemberId, string RequesterName,
    Guid? CurrentApproverMemberId, string? DenialReason, DateTime CreatedAtUtc,
    Dictionary<string, JsonElement>? Answers = null, Guid? ExecutionPlanId = null,
    List<PendingRequestQuestionDto>? PendingQuestions = null);

public sealed record PendingRequestQuestionDto(Guid Id, string QuestionText, bool IsAnswered, string? Answer);


public sealed record RequestTypeOptionDto(Guid Id, string Code, string Name, string? Description, List<JsonElement> Questions);

public sealed record ExecutionPlanDto(Guid Id, Guid RequestId, string Status, List<ExecutionItemDto> Items);
public sealed record ExecutionItemDto(Guid Id, string Type, string Title, Guid? AssigneeMemberId, bool IsSelected, Guid? ResultingEntityId);
