using System.Net;
using System.Text.Json;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Services;

public sealed class FamilyApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public FamilyApiException(HttpStatusCode statusCode, string message)
        : base(message) => StatusCode = statusCode;
}

/// <summary>
/// Authenticated client for the existing FamilyOS API. It deliberately does not
/// invent client-side household or role context; the API derives both from the JWT.
/// </summary>
public sealed class PulseApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;
    private readonly StartupDiagnosticsService _diagnostics;

    public PulseApiClient(HttpClient http, IAuthSessionService auth, StartupDiagnosticsService diagnostics)
    {
        _http = http;
        _auth = auth;
        _diagnostics = diagnostics;
    }

    public async Task<PulseDto> GetPulseAsync(CancellationToken cancellationToken = default)
    {
        _diagnostics.RecordCheckpoint("AUTH API pulse request started; acquiring access token");
        var token = await _auth.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            _diagnostics.RecordCheckpoint("AUTH API pulse request blocked; no access token available");
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to load your family dashboard.");
        }
        _diagnostics.RecordCheckpoint("AUTH API pulse access token available; token value omitted");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiBaseUri, "api/pulse"));
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        _diagnostics.RecordCheckpoint($"AUTH API pulse response; http_status={(int)response.StatusCode}; success={response.IsSuccessStatusCode}");
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, timeout.Token);
            _diagnostics.RecordCheckpoint($"AUTH API pulse rejected; status={(int)response.StatusCode}");
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var pulse = await JsonSerializer.DeserializeAsync<PulseDto>(stream, JsonOptions, timeout.Token);
        if (pulse is null)
        {
            _diagnostics.RecordCheckpoint("AUTH API pulse response was empty or invalid");
            throw new InvalidOperationException("The FamilyOS API returned an empty Pulse response.");
        }
        _diagnostics.RecordCheckpoint("AUTH API pulse deserialization succeeded");
        return pulse;
    }

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            foreach (var property in new[] { "detail", "error", "title" })
            {
                if (document.RootElement.TryGetProperty(property, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Fall through to the status-based message for non-JSON error responses.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Sign in again.",
            HttpStatusCode.Forbidden => "Your account does not have access to this family information.",
            HttpStatusCode.NotFound => "The dashboard endpoint was not found.",
            _ => $"FamilyOS could not load the dashboard (HTTP {(int)response.StatusCode})."
        };
    }
}

public sealed record PulseDto(
    string Greeting,
    string Role,
    string? RoleHint,
    PulseNextActionDto? NextAction,
    List<PulseAttentionItemDto> NeedsAttention,
    List<PulseAttentionItemDto> WaitingBlockers,
    List<PulseUpcomingItemDto> ComingUp,
    PulseHouseholdSummaryDto Household,
    PulseProcurementSummaryDto? Procurement,
    List<PulseConditionItemDto> Conditions);

public sealed record PulseNextActionDto(
    string Kind, Guid EntityId, string Title, string? Subtitle, string PrimaryAction, string? DueLabel);

public sealed record PulseAttentionItemDto(
    string Kind, Guid EntityId, string Title, string Reason, string ActionLabel);

public sealed record PulseUpcomingItemDto(
    string Kind, Guid EntityId, string Title, DateTime WhenUtc, string? Location);

public sealed record PulseHouseholdSummaryDto(
    int RequestsAwaitingApproval, int TasksDueToday, int TasksNeedingAcceptance, string? NextShoppingTrip);

public sealed record PulseProcurementSummaryDto(int ApprovedItems, int ReadyForStore, string? PrimaryStore);

public sealed record PulseConditionItemDto(Guid Id, string Name, string Status, string? RelatedTitle);
