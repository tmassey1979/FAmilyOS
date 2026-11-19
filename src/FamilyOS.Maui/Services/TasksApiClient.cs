using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Services;

/// <summary>Authenticated access to the existing task endpoints.</summary>
public sealed class TasksApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public TasksApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public Task<List<FamilyTaskDto>> GetMyTasksAsync(CancellationToken cancellationToken = default)
        => SendAsync<List<FamilyTaskDto>>(HttpMethod.Get, "api/tasks/mine", null, cancellationToken);

    public Task<FamilyTaskDto> RunActionAsync(Guid taskId, string action, CancellationToken cancellationToken = default)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "accept", "start", "pause", "resume", "complete"
        };
        if (!allowed.Contains(action))
            throw new ArgumentOutOfRangeException(nameof(action), "Unsupported task action.");

        return SendAsync<FamilyTaskDto>(HttpMethod.Post, $"api/tasks/{taskId:D}/{action}", null, cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var token = await _auth.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to view your tasks.");

        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session has expired. Sign in again.",
                HttpStatusCode.Forbidden => "You do not have permission to perform that task action.",
                HttpStatusCode.NotFound => "That task could not be found.",
                _ => $"FamilyOS could not complete the task action (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token);
        return result ?? throw new InvalidOperationException("The FamilyOS API returned an empty task response.");
    }
}

public sealed record FamilyTaskDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? AssignedToMemberId,
    string? AssignedToName,
    DateTime? DueDate,
    int? EstimatedDurationMinutes,
    int? ActualDurationMinutes,
    string? Category,
    bool CalendarBacked,
    DateTime CreatedAtUtc,
    bool IsOverdue,
    string? PlannedStartTime,
    string? PlannedEndTime,
    int? VarianceMinutes,
    Guid? RecurrenceDefinitionId = null,
    bool IsRecurring = false,
    string? RecurrenceFrequency = null);
