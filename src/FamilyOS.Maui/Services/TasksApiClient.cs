using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

    public Task<List<FamilyTaskDto>> GetFamilyTasksAsync(CancellationToken cancellationToken = default)
        => SendAsync<List<FamilyTaskDto>>(HttpMethod.Get, "api/tasks", null, cancellationToken);

    public Task<List<RecurrenceDto>> GetRecurrencesAsync(CancellationToken cancellationToken = default)
        => SendAsync<List<RecurrenceDto>>(HttpMethod.Get, "api/tasks/recurrences", null, cancellationToken);

    public Task<RecurrenceDto> CreateRecurrenceAsync(CreateRecurrenceBody body, CancellationToken cancellationToken = default)
        => SendAsync<RecurrenceDto>(HttpMethod.Post, "api/tasks/recurrences", JsonBody(body), cancellationToken);

    public Task<RecurrenceDto> DeactivateRecurrenceAsync(Guid recurrenceId, CancellationToken cancellationToken = default)
        => SendAsync<RecurrenceDto>(HttpMethod.Post, $"api/tasks/recurrences/{recurrenceId:D}/deactivate", null, cancellationToken);

    public Task<FamilyTaskDto> CreateTaskAsync(CreateTaskBody body, CancellationToken cancellationToken = default)
        => SendAsync<FamilyTaskDto>(HttpMethod.Post, "api/tasks", JsonBody(body), cancellationToken);

    public Task<List<TaskHistoryDto>> GetHistoryAsync(Guid taskId, CancellationToken cancellationToken = default)
        => SendAsync<List<TaskHistoryDto>>(HttpMethod.Get, $"api/tasks/{taskId:D}/history", null, cancellationToken);

    public Task<FamilyTaskDto> UpdateDetailsAsync(Guid taskId, UpdateTaskDetailsBody body, CancellationToken cancellationToken = default)
        => SendAsync<FamilyTaskDto>(HttpMethod.Put, $"api/tasks/{taskId:D}", JsonBody(body), cancellationToken);

    public Task<FamilyTaskDto> SetPlannedTimesAsync(Guid taskId, PlannedTimesBody body, CancellationToken cancellationToken = default)
        => SendAsync<FamilyTaskDto>(HttpMethod.Put, $"api/tasks/{taskId:D}/planned-times", JsonBody(body), cancellationToken);

    /// <summary>Simple lifecycle actions without a body (accept/start/pause/resume/complete).</summary>
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

    public Task<FamilyTaskDto> DeferAsync(Guid taskId, string? note = null, CancellationToken cancellationToken = default)
        => SendAsync<FamilyTaskDto>(HttpMethod.Post, $"api/tasks/{taskId:D}/defer", JsonBody(new NoteBody(note, null)), cancellationToken);

    public Task<FamilyTaskDto> CancelAsync(Guid taskId, string? reason = null, CancellationToken cancellationToken = default)
        => SendAsync<FamilyTaskDto>(HttpMethod.Post, $"api/tasks/{taskId:D}/cancel", JsonBody(new NoteBody(null, reason)), cancellationToken);

    public Task<FamilyTaskDto> SkipAsync(Guid taskId, string? note = null, CancellationToken cancellationToken = default)
        => SendAsync<FamilyTaskDto>(HttpMethod.Post, $"api/tasks/{taskId:D}/skip", JsonBody(new NoteBody(note, null)), cancellationToken);

    private static StringContent JsonBody<T>(T value)
        => new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

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
                HttpStatusCode.Conflict => "That task action is not allowed in the current state.",
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

public sealed record RecurrenceDto(
    Guid Id, string Title, string? Description, string Frequency, int Interval,
    string? DayOfWeek, Guid? DefaultAssigneeMemberId, string DeclinePolicy,
    bool IsActive, DateTime? NextOccurrenceUtc, string? Category);

public sealed record CreateRecurrenceBody(
    string Title, string Frequency, int Interval = 1, string? DayOfWeek = null,
    Guid? DefaultAssigneeMemberId = null, string? Category = null,
    string? Description = null, string DeclinePolicy = "NoChange", bool GenerateFirstInstance = true);

public sealed record CreateTaskBody(
    string Title,
    string? Description = null,
    string Priority = "Normal",
    string? Category = null,
    DateTime? DueDate = null,
    int? EstimatedDurationMinutes = null,
    Guid? AssignToMemberId = null);

public sealed record UpdateTaskDetailsBody(
    string? Title = null,
    string? Description = null,
    string? Priority = null,
    string? Category = null,
    DateTime? DueDate = null,
    bool ClearDueDate = false);

public sealed record PlannedTimesBody(
    string? PlannedStartTime = null,
    string? PlannedEndTime = null,
    int? EstimatedDurationMinutes = null);

public sealed record TaskHistoryDto(
    string Action,
    string? Detail,
    DateTime OccurredAtUtc,
    Guid? ActorMemberId = null,
    string? ActorName = null);

internal sealed record NoteBody(string? Note, string? Reason);
