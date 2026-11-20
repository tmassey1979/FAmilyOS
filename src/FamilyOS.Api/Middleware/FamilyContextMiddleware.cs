using Microsoft.Extensions.Logging;
using System.Diagnostics;
using FamilyOS.Application.Interfaces;

namespace FamilyOS.Api.Middleware;

/// <summary>
/// After authentication, resolve User → FamilyMember → Role into ICurrentUserService.
/// </summary>
public sealed class FamilyContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FamilyContextMiddleware> _logger;

    public FamilyContextMiddleware(RequestDelegate next, ILogger<FamilyContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        var elapsed = Stopwatch.StartNew();
        var authenticated = context.User.Identity?.IsAuthenticated == true;
        _logger.LogInformation(
            "Request pipeline: family context entered. Method={Method} Path={Path} Authenticated={Authenticated} TraceId={TraceId}",
            context.Request.Method, context.Request.Path, authenticated, context.TraceIdentifier);

        try
        {
            if (authenticated)
            {
                var load = Stopwatch.StartNew();
                await currentUser.EnsureLoadedAsync(context.RequestAborted);
                _logger.LogInformation(
                    "Request pipeline: family context loaded in {ElapsedMs}ms. HasUser={HasUser} HasFamily={HasFamily} TraceId={TraceId}",
                    load.Elapsed.TotalMilliseconds, currentUser.UserId.HasValue, currentUser.FamilyId.HasValue, context.TraceIdentifier);
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Request pipeline: family context/downstream failed after {ElapsedMs}ms. TraceId={TraceId}",
                elapsed.Elapsed.TotalMilliseconds, context.TraceIdentifier);
            throw;
        }
        finally
        {
            _logger.LogInformation(
                "Request pipeline: family context completed in {ElapsedMs}ms. StatusCode={StatusCode} TraceId={TraceId}",
                elapsed.Elapsed.TotalMilliseconds, context.Response.StatusCode, context.TraceIdentifier);
        }
    }
}
