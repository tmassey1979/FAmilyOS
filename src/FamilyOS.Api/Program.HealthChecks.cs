using FamilyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal record UpdateQuietHoursBody(bool Enabled, int? StartMinutes, int? EndMinutes);
internal record RegisterDeviceBody(string Token, string Platform, string? DeviceName);

internal sealed class KeycloakSigningKeysHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtOptions;
    private readonly string _expectedIssuer;
    private readonly ILogger<KeycloakSigningKeysHealthCheck> _logger;

    public KeycloakSigningKeysHealthCheck(
        IOptionsMonitor<JwtBearerOptions> jwtOptions,
        IConfiguration configuration,
        ILogger<KeycloakSigningKeysHealthCheck> logger)
    {
        _jwtOptions = jwtOptions;
        _expectedIssuer = configuration["Keycloak:Authority"]
            ?? "http://localhost:8080/realms/familyos";
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var manager = _jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme).ConfigurationManager;
            if (manager is null)
                return HealthCheckResult.Unhealthy("OIDC configuration manager is not initialized");

            var metadata = await manager.GetConfigurationAsync(cancellationToken);
            var signingKeyCount = metadata.SigningKeys?.Count() ?? 0;

            if (!string.Equals(metadata.Issuer, _expectedIssuer, StringComparison.Ordinal))
            {
                _logger.LogError(
                    "OIDC issuer mismatch. ExpectedIssuer={ExpectedIssuer} MetadataIssuer={MetadataIssuer} SigningKeyCount={SigningKeyCount}",
                    _expectedIssuer, metadata.Issuer, signingKeyCount);
                return HealthCheckResult.Unhealthy(
                    "OIDC metadata issuer does not match the configured authority",
                    data: new Dictionary<string, object>
                    {
                        ["expectedIssuer"] = _expectedIssuer,
                        ["metadataIssuer"] = metadata.Issuer ?? string.Empty,
                        ["signingKeyCount"] = signingKeyCount
                    });
            }

            if (signingKeyCount == 0)
            {
                _logger.LogError(
                    "OIDC metadata loaded but contains no signing keys. Issuer={Issuer}",
                    metadata.Issuer);
                return HealthCheckResult.Unhealthy(
                    "OIDC metadata contains no signing keys",
                    data: new Dictionary<string, object>
                    {
                        ["issuer"] = metadata.Issuer,
                        ["signingKeyCount"] = signingKeyCount
                    });
            }

            _logger.LogDebug(
                "OIDC signing keys are ready. Issuer={Issuer} SigningKeyCount={SigningKeyCount}",
                metadata.Issuer, signingKeyCount);
            return HealthCheckResult.Healthy(
                "OIDC discovery and signing keys are available",
                data: new Dictionary<string, object>
                {
                    ["issuer"] = metadata.Issuer,
                    ["signingKeyCount"] = signingKeyCount
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unable to load OIDC discovery metadata or signing keys. ExpectedIssuer={ExpectedIssuer}",
                _expectedIssuer);
            return HealthCheckResult.Unhealthy(
                "Unable to retrieve OIDC discovery metadata or signing keys", ex);
        }
    }
}

internal sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PostgresHealthCheck(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FamilyOsDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Postgres reachable")
                : HealthCheckResult.Unhealthy("Postgres not reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres check failed", ex);
        }
    }
}
