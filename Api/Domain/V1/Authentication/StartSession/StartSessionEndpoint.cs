using Api.Domain.Dtos;
using Api.Shared;
using Api.Shared.Authentication;
using Api.Shared.Caching;
using Api.Shared.Dtos;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace Api.Domain.V1.Authentication.StartSession;

public class StartSessionEndpoint : Endpoint<StartSessionRequestDto, StartSessionResponseDto>
{
    private readonly JwtService _jwtService;
    private readonly SessionTrackingService _sessionTracker;
    private readonly SessionSettings _sessionSettings;

    public StartSessionEndpoint(
        JwtService jwtService,
        SessionTrackingService sessionTracker,
        IOptions<SessionSettings> sessionSettings)
    {
        _jwtService = jwtService;
        _sessionTracker = sessionTracker;
        _sessionSettings = sessionSettings.Value;
    }

    public override void Configure()
    {
        Post("v1/api/session/start");
        AllowAnonymous();
        DontCatchExceptions();
        Options(x => x.Produces<StartSessionResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
        );
    }

    public override async Task HandleAsync(StartSessionRequestDto req, CancellationToken ct)
    {
        // Create a new session
        var session = UserSession.Create(_sessionSettings.SessionDurationMinutes);

        // Store in Redis
        await _sessionTracker.StoreSessionAsync(session);

        // Create JWT token
        var token = _jwtService.EncodeToken(session);
        
        // Store the session in the HttpContext for use in endpoints
        HttpContext.Response.Headers.Append(Constants.ResponseHeadersTokenKey, token);

        // Return the token
        await SendAsync(new StartSessionResponseDto()
        {
            SessionName = session.SessionName.ToString(),
            Token = token,
            ExpiryTime = session.ExpiryTime
        }, cancellation: ct);
    }
}