using Api.Domain.Dtos;
using Api.Shared.Authentication;
using Api.Shared.Caching;
using Api.Shared.Dtos;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace Api.Domain.V1.Authentication.EndSession;

public class EndSessionEndpoint : Endpoint<EndSessionRequestDto, EndSessionResponseDto>
{
    private readonly JwtService _jwtService;
    private readonly SessionTrackingService _sessionTracker;
    private readonly SessionSettings _sessionSettings;

    public EndSessionEndpoint(
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
        Post("v1/api/session/end");
        AllowAnonymous();
        DontCatchExceptions();
        Options(x => x.Produces<EndSessionResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
        );
    }
    
    public override async Task HandleAsync(EndSessionRequestDto req, CancellationToken ct)
    {
        // Remove from Redis regardless of expiry
        await _sessionTracker.RemoveSessionAsync(Guid.Parse(req.SessionName));
            
        await SendOkAsync(ct);
    }
}