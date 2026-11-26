using System.Text;
using System.Security.Claims;
using SecurityClaim = System.Security.Claims.Claim;
using System.IdentityModel.Tokens.Jwt;
using JsonWebToken = Microsoft.IdentityModel.JsonWebTokens.JsonWebToken;
using JwtRegisteredClaimNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FamilyOS.Api.Hubs;
using FamilyOS.Api.Middleware;
using FamilyOS.Api.Observability;
using FamilyOS.Application.Calendar;
using FamilyOS.Application.Family;
using FamilyOS.Application.Interfaces;
using FamilyOS.Application.Notifications;
using FamilyOS.Application.Tasks;
using FamilyOS.Infrastructure;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Persistence;
using FamilyOS.Infrastructure.Seed;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(FamilyOS.Application.Family.ProfileCommands).Assembly));
    builder.Services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "FamilyOS API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
        ?? "http://localhost:8080/realms/familyos";

    var useDevBypass = builder.Environment.IsDevelopment()
        || string.Equals(builder.Configuration["Auth:UseDevBypass"], "true", StringComparison.OrdinalIgnoreCase);

    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        if (useDevBypass)
        {
            options.DefaultAuthenticateScheme = "Smart";
            options.DefaultChallengeScheme = "Smart";
        }
        else
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }
    });

    authBuilder.AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Authority = keycloakAuthority;
        options.RequireHttpsMetadata = false;
        var metadata = builder.Configuration["Keycloak:MetadataAddress"];
        if (!string.IsNullOrWhiteSpace(metadata))
            options.MetadataAddress = metadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakAuthority,
            ValidateAudience = false,
            ValidateLifetime = true,
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };
    });

    if (useDevBypass)
    {
        authBuilder.AddScheme<AuthenticationSchemeOptions, DevBypassAuthHandler>("DevBypass", _ => { });
        authBuilder.AddPolicyScheme("Smart", "Smart", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.ContainsKey("X-Dev-User"))
                    return "DevBypass";
                return JwtBearerDefaults.AuthenticationScheme;
            };
        });
    }

    builder.Services.AddAuthorization(options =>
    {
        FamilyAuthPolicies.Register(options);
    });

    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IFamilyChatNotifier, SignalRFamilyChatNotifier>();

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("API process is running"))
        .AddCheck<PostgresHealthCheck>("postgres")
        .AddCheck<KeycloakSigningKeysHealthCheck>("keycloak-signing-keys");

    builder.Services.AddHttpClient("posthog");
    builder.Services.AddSingleton<IPostHogErrorReporter, PostHogErrorReporter>();

    var app = builder.Build();

    using (var migrationScope = app.Services.CreateScope())
    {
        var migrationDb = migrationScope.ServiceProvider.GetRequiredService<FamilyOsDbContext>();
        var migrationLogger = migrationScope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");
        var applyMigrations = app.Configuration.GetValue("Database:MigrateOnStartup", true);
        await DatabaseStartup.RunAsync(migrationDb, migrationLogger, applyMigrations);
    }

    var fwd = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
    };
    fwd.KnownNetworks.Clear();
    fwd.KnownProxies.Clear();
    app.UseForwardedHeaders(fwd);

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseHttpMetrics();

    app.MapControllers();
    app.MapHub<FamilyChatHub>("/hubs/chat");
    app.MapMetrics();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = WriteMinimalHealth
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready") || check.Name is "self" or "postgres" or "keycloak-signing-keys",
        ResponseWriter = WriteDetailedHealth
    });

    app.MapGet("/api/me", async (ICurrentUserService current, CancellationToken ct) =>
    {
        await current.EnsureLoadedAsync(ct);
        return Results.Ok(new
        {
            current.ExternalIdentityId,
            current.UserId,
            current.FamilyId,
            current.MemberId,
            Role = current.Role?.ToString()
        });
    }).RequireAuthorization();

    app.MapGet("/api/me/quiet-hours", async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(new GetQuietHoursQuery(), ct)))
        .RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapPut("/api/me/quiet-hours", async (UpdateQuietHoursBody body, IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(new UpdateQuietHoursCommand(body.Enabled, body.StartMinutes, body.EndMinutes), ct)))
        .RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapPost("/api/me/devices", async (RegisterDeviceBody body, IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(new RegisterDeviceCommand(body.Token, body.Platform, body.DeviceName), ct)))
        .RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapGet("/api/me/devices", async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(new GetMyDevicesQuery(), ct)))
        .RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapDelete("/api/me/devices", async (string token, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.Send(new UnregisterDeviceCommand(token), ct);
        return Results.NoContent();
    }).RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapGet("/api/me/google-calendar", async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(new GetGoogleCalendarStatusQuery(), ct)))
        .RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapGet("/api/me/google-calendar/auth-url", async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(new { url = await mediator.Send(new GetGoogleCalendarAuthUrlQuery(), ct) }))
        .RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapDelete("/api/me/google-calendar", async (IMediator mediator, CancellationToken ct) =>
    {
        await mediator.Send(new DisconnectGoogleCalendarCommand(), ct);
        return Results.NoContent();
    }).RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapPost("/api/me/google-calendar/sync", async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(new { changed = await mediator.Send(new TriggerGoogleCalendarSyncCommand(), ct) }))
        .RequireAuthorization(FamilyAuthPolicies.FamilyMember);

    app.MapGet("/api/integrations/google/callback", async (string? code, string? state, string? error, IMediator mediator) =>
    {
        if (!string.IsNullOrWhiteSpace(error))
            return Results.BadRequest(new { error });
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Results.BadRequest(new { error = "missing code or state" });
        await mediator.Send(new CompleteGoogleCalendarOAuthCommand(code, state));
        return Results.Ok(new { ok = true });
    }).AllowAnonymous();

    // Static avatars
    var avatarRoot = Path.Combine(app.Environment.ContentRootPath, "avatars");
    Directory.CreateDirectory(avatarRoot);
    app.MapGet("/avatars/{fileName}", (string fileName) =>
    {
        var safe = Path.GetFileName(fileName);
        var path = Path.Combine(avatarRoot, safe);
        if (!System.IO.File.Exists(path))
            return Results.NotFound();
        var ext = Path.GetExtension(safe).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
        return Results.File(path, contentType);
    }).AllowAnonymous();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static Task WriteMinimalHealth(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "text/plain";
    return ctx.Response.WriteAsync(report.Status == HealthStatus.Healthy ? "Healthy" : report.Status.ToString());
}

static async Task WriteDetailedHealth(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        entries = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds
            })
    };
    await ctx.Response.WriteAsJsonAsync(payload);
}

file record UpdateQuietHoursBody(bool Enabled, int? StartMinutes, int? EndMinutes);
file record RegisterDeviceBody(string Token, string Platform, string? DeviceName);

file sealed class PostgresHealthCheck : IHealthCheck
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

file sealed class KeycloakSigningKeysHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public KeycloakSigningKeysHealthCheck(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var authority = _config["Keycloak:Authority"] ?? "http://localhost:8080/realms/familyos";
            var jwksUrl = authority.TrimEnd('/') + "/protocol/openid-connect/certs";
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync(jwksUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return HealthCheckResult.Unhealthy($"Keycloak JWKS returned {(int)response.StatusCode}");
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body) || !body.Contains("keys", StringComparison.OrdinalIgnoreCase))
                return HealthCheckResult.Unhealthy("Keycloak JWKS response missing keys");
            return HealthCheckResult.Healthy("Keycloak signing keys reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Keycloak signing keys check failed", ex);
        }
    }
}

public partial class Program { }
