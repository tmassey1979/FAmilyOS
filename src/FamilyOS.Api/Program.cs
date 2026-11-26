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
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(TaskCommandHandlers).Assembly));
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
        c.AddSecurityDefinition("DevBypass", new OpenApiSecurityScheme
        {
            Description = "Dev bypass header X-Dev-User",
            Name = "X-Dev-User",
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
        authBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, _ => { });
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

    builder.Services.AddFamilyAuthorization();

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

    // Keep API documentation available in production during the current rollout.
    // Set Swagger:Enabled=false (environment variable Swagger__Enabled=false) at launch to disable it.
    if (app.Configuration.GetValue("Swagger:Enabled", true))
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "FamilyOS API v1");
            options.RoutePrefix = "swagger";
        });
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

    app.MapGet("/api/integrations/google/callback", async (string? code, string? state, string? error, IMediator mediator, ICurrentUserService current, CancellationToken ct) =>
    {
        if (!string.IsNullOrWhiteSpace(error))
            return Results.BadRequest(new { error });
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Results.BadRequest(new { error = "missing code or state" });
        await current.EnsureLoadedAsync(ct);
        var memberId = current.MemberId ?? Guid.Empty;
        var familyId = current.FamilyId ?? Guid.Empty;
        var status = await mediator.Send(new CompleteGoogleCalendarOAuthCommand(code, memberId, familyId), ct);
        return Results.Ok(new { ok = true, status });
    }).AllowAnonymous();

    var seedEnabled = app.Environment.IsDevelopment()
        || string.Equals(app.Configuration["Seed:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    if (seedEnabled)
        await SeedData.InitializeAsync(app.Services);

    using (var schemaScope = app.Services.CreateScope())
    {
        var db = schemaScope.ServiceProvider.GetRequiredService<FamilyOsDbContext>();
        var schemaLog = schemaScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SchemaEnsure");
        await EnsureFamilyInvitesSchema.RunAsync(db, schemaLog);
        await EnsureMealSchema.RunAsync(db, schemaLog);
        await EnsureRecipeSchema.RunAsync(db, schemaLog);
        await EnsureRecipeCatalogSchema.RunAsync(db, schemaLog);
        await EnsureMealNominationSchema.RunAsync(db, schemaLog);
        await EnsureLocationSchema.RunAsync(db, schemaLog);
        await EnsureAvatarSchema.RunAsync(db, schemaLog);
        await EnsureTaskSchema.RunAsync(db, schemaLog);
    }

    var avatarsRoot = Path.Combine(app.Environment.ContentRootPath, "avatars");
    Directory.CreateDirectory(avatarsRoot);
    app.MapGet("/avatars/{fileName}", (string fileName) =>
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
            return Results.BadRequest();
        var path = Path.Combine(avatarsRoot, fileName);
        if (!System.IO.File.Exists(path))
            return Results.NotFound();
        var contentType = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : "image/jpeg";
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

public partial class Program { }
