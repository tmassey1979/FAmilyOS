using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Text.Json.Serialization;
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
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "FamilyOS.Api")
        .WriteTo.Console());

    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(TaskCommandHandlers).Assembly));

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Family OS API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityDefinition("DevBypass", new OpenApiSecurityScheme
        {
            Description = "Dev only: X-Dev-User header (e.g. terry.owner).",
            Name = DevAuthenticationHandler.HeaderName,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
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
        options.Authority = keycloakAuthority;
        options.RequireHttpsMetadata = false;
        // Docker: authority is the public issuer (https://auth…); metadata is fetched on the internal network
        var metadata = builder.Configuration["Keycloak:MetadataAddress"];
        if (!string.IsNullOrWhiteSpace(metadata))
            options.MetadataAddress = metadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakAuthority,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Some JWT handler configurations expose the validated subject on the
                // SecurityToken but omit both the raw "sub" and mapped NameIdentifier
                // claims from ClaimsPrincipal. Restore only from the already-validated
                // token subject; never fall back to a mutable username or email.
                var principal = context.Principal;
                var hasSubjectClaim = principal?.HasClaim(c => c.Type == "sub") == true;
                var hasNameIdentifier = principal?.HasClaim(c => c.Type == ClaimTypes.NameIdentifier) == true;
                var tokenSubject = context.SecurityToken switch
                {
                    JwtSecurityToken jwt => jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value,
                    JsonWebToken json => json.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value,
                    _ => null
                };

                if (!hasSubjectClaim && !hasNameIdentifier && !string.IsNullOrWhiteSpace(tokenSubject))
                {
                    var identity = principal?.Identities.FirstOrDefault(i => i.IsAuthenticated);
                    identity?.AddClaim(new System.Security.Claims.Claim("sub", tokenSubject!));
                    Log.Warning(
                        "JWT validated principal omitted subject claims; restored sub from validated security token. Path={Path} TraceId={TraceId}",
                        context.Request.Path, context.HttpContext.TraceIdentifier);
                }
                else if (!hasSubjectClaim && !hasNameIdentifier)
                {
                    var claimTypes = principal is null
                        ? "none"
                        : string.Join(",", principal.Claims.Select(c => c.Type).Distinct().OrderBy(x => x));
                    Log.Error(
                        "JWT validated without a usable subject. SecurityTokenSubjectPresent={SubjectPresent} ClaimTypes={ClaimTypes} Path={Path} TraceId={TraceId}",
                        !string.IsNullOrWhiteSpace(tokenSubject), claimTypes,
                        context.Request.Path, context.HttpContext.TraceIdentifier);
                }

                Log.Information(
                    "JWT authentication succeeded. HasSub={HasSub} HasNameIdentifier={HasNameIdentifier} Path={Path} TraceId={TraceId}",
                    principal?.HasClaim(c => c.Type == "sub") == true,
                    principal?.HasClaim(c => c.Type == ClaimTypes.NameIdentifier) == true,
                    context.Request.Path, context.HttpContext.TraceIdentifier);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Log.Warning(context.Exception, "JWT authentication failed. Path={Path} TraceId={TraceId}",
                    context.Request.Path, context.HttpContext.TraceIdentifier);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Log.Warning("JWT authentication challenge. Error={Error} Path={Path} TraceId={TraceId}",
                    context.Error, context.Request.Path, context.HttpContext.TraceIdentifier);
                return Task.CompletedTask;
            }
        };
    });

    if (useDevBypass)
    {
        authBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
            DevAuthenticationHandler.SchemeName, _ => { });

        authBuilder.AddPolicyScheme("Smart", "JWT or DevBypass", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.ContainsKey(DevAuthenticationHandler.HeaderName))
                    return DevAuthenticationHandler.SchemeName;
                return JwtBearerDefaults.AuthenticationScheme;
            };
        });
    }

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

    var fwd = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
    };
    fwd.KnownNetworks.Clear();
    fwd.KnownProxies.Clear();
    app.UseForwardedHeaders(fwd);

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, http) =>
        {
            diag.Set("RequestHost", http.Request.Host.Value);
            diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Family OS API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
    });

    app.UseCors();
    app.UseAuthentication();
    app.UseMiddleware<FamilyContextMiddleware>();
    app.UseAuthorization();
    app.UseHttpMetrics();
    app.MapControllers();
    app.MapHub<FamilyChatHub>("/hubs/chat");

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = r => r.Name == "self",
        ResponseWriter = WriteMinimalHealth
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        ResponseWriter = WriteDetailedHealth
    });

    app.MapMetrics();

    app.MapGet("/", () => Results.Redirect("/swagger"));

    app.MapGet("/api/me", async (ICurrentUserService current) =>
    {
        await current.EnsureLoadedAsync();
        if (!current.IsAuthenticated)
            return Results.Unauthorized();
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
            return Results.BadRequest(new { error = "code and state are required" });

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(state));
            var parts = raw.Split('|');
            if (parts.Length < 2
                || !Guid.TryParse(parts[0], out var memberId)
                || !Guid.TryParse(parts[1], out var familyId))
            {
                return Results.BadRequest(new { error = "invalid state" });
            }

            var status = await mediator.Send(new CompleteGoogleCalendarOAuthCommand(code, memberId, familyId));
            return Results.Content(
                "<!DOCTYPE html><html><body style=\"font-family:system-ui;padding:2rem\">" +
                "<h2>Google Calendar connected</h2>" +
                $"<p>{(status.Email ?? "account")} is linked. You can close this window and return to Family OS.</p>" +
                "</body></html>",
                "text/html");
        }
        catch (Exception ex)
        {
            return Results.Content(
                "<!DOCTYPE html><html><body style=\"font-family:system-ui;padding:2rem\">" +
                $"<h2>Connection failed</h2><p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>",
                "text/html");
        }
    }).AllowAnonymous();

    var seedEnabled = app.Environment.IsDevelopment()
        || string.Equals(app.Configuration["Seed:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    if (seedEnabled)
    {
        try { await SeedData.InitializeAsync(app.Services); }
        catch (Exception ex) { Log.Warning(ex, "Seed failed — ensure Postgres is up."); }
    }

    try
    {
        using var schemaScope = app.Services.CreateScope();
        var db = schemaScope.ServiceProvider.GetRequiredService<FamilyOsDbContext>();
        var schemaLog = schemaScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SchemaEnsure");
        await EnsureFamilyInvitesSchema.RunAsync(db, schemaLog);
        await EnsureMealSchema.RunAsync(db, schemaLog);
        await EnsureRecipeSchema.RunAsync(db, schemaLog);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Schema ensure failed");
    }

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

file sealed class KeycloakSigningKeysHealthCheck : IHealthCheck
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

public partial class Program { }
