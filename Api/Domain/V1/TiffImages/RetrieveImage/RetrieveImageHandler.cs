using Api.Domain.V1.TiffImages.ListAllImages;
using Api.Shared;
using Api.Shared.Messaging;
using FastEndpoints;

namespace Api.Domain.V1.TiffImages.RetrieveImage;

public class RetrieveImagesEndpoint(IEventPublisher eventPublisher) : EndpointWithoutRequest<RetrieveResponseDto>
{
    public const string TiffFileStoragePath = @"D:\New folder\Tiffster\Api\Domain\V1\TiffImages\Images";
    
    public override void Configure()
    {
        Get("v1/api/images/{index}");
        AllowAnonymous();
        DontCatchExceptions();
        Options(x => x.Produces<RetrieveResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var index = Route<string>("index");
        
        // Check if token was renewed by middleware
        HttpContext.CheckIfTokenChanged();
        
        var response = new RetrieveResponseDto
        {
            Id = Guid.NewGuid()
        };
        
        await SendAsync(response, cancellation: ct);
    }
}