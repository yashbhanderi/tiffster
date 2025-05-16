using Api.Shared.Caching;
using Api.Shared.Dtos;
using Microsoft.Extensions.Options;

namespace Api.Shared.Authentication;

public class SessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _excludedPaths;
    private readonly IOptions<SessionSettings> _sessionSettings;

    public SessionMiddleware(RequestDelegate next, IOptions<SessionSettings> sessionSettings)
    {
        _next = next;
        _sessionSettings = sessionSettings;
        _excludedPaths = sessionSettings.Value.ExcludedPaths;
    }

    public async Task InvokeAsync(HttpContext context, JwtService jwtService, SessionTrackingService sessionTracker)
    {
        // Skip token validation for excluded paths (i.e. StartSession)
        var path = context.Request.Path.Value?.ToLower();
        if (path is not null && _excludedPaths.Any(ep => path.StartsWith(ep, StringComparison.CurrentCultureIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Extract token from Authorization header
        var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(" ").Last();

        if (token.IsEmpty())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Unauthorized: No token provided");
            return;
        }

        // Validate token
        var (session, isValid) = jwtService.DecodeAndValidateToken(token);

        if (!isValid || session == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Unauthorized: Invalid token");
            return;
        }
        
        // Check if session exists in Redis
        if (!await sessionTracker.SessionExistsAsync(session.SessionName))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Unauthorized: Invalid token");
            return;
        }
        
        bool isTokenChanged = false;

        // Store the session in the HttpContext for use in endpoints
        context.Items["UserSession"] = session;

        // Check if token has expired and needs renewal
        if (session.HasExpired())
        {
            // Create renewed session with same session name
            var renewedSession = session.ExtendSession(_sessionSettings.Value.RenewalDurationMinutes);

            // Store new token in Redis
            await sessionTracker.StoreSessionAsync(renewedSession);

            // Create new token
            var newToken = jwtService.EncodeToken(renewedSession);

            // Update session in HttpContext
            context.Request.HttpContext.Items["UserSession"] = renewedSession;
            context.Request.HttpContext.Items["NewToken"] = newToken;
        }

        // Process the request
        await _next(context);
    }
}