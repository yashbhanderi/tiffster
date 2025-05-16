using Api.Domain.Dtos;
using Api.Domain.V1.TiffImages.Dtos;
using Api.Shared;
using Api.Shared.Messaging;
using FastEndpoints;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Domain.V1.TiffImages.ListAllImages;

public class ListAllImagesEndpoint(
    IEventPublisher eventPublisher,
    IMemoryCache memoryCache,
    ITiffFileHelper tiffFileHelper,
    IGoogleDriveService googleDriveService) : Endpoint<ListAllImagesRequestDto, ListAllImagesResponseDto>
{
    public override void Configure()
    {
        Get("v1/api/images");
        AllowAnonymous();
        DontCatchExceptions();
        Options(x => x.Produces<ListAllImagesResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
        );
    }

    public override async Task HandleAsync(ListAllImagesRequestDto req, CancellationToken ct)
    {
        HttpContext.CheckIfTokenChanged();

        var response = new ListAllImagesResponseDto { TiffImages = new List<TiffImage>() };
        var pageNumber = req.PageNumber;
        var tiffFilePath = Path.Combine(Constants.TiffFileStoragePath, $"{req.SessionName}.tif");

        IEnumerable<TiffImage>? tiffImages = null;

        if (pageNumber != null && memoryCache.TryGetValue(pageNumber, out var cached) &&
            cached is List<TiffImage> cachedList)
        {
            if (cachedList.First().FileUrl.IsEmpty())
            {
                response.TiffImages = await tiffFileHelper.UploadImagesByPageNumberAsync(tiffFilePath, new List<long>() { (long)pageNumber },
                        ct);
            }
            else
            {
                response = new ListAllImagesResponseDto
                {
                    TiffImages = cachedList // clone to prevent side effects
                };
            }
        }
        else
        {
            if (!File.Exists(tiffFilePath))
            {
                await googleDriveService.DownloadFileAsync(req.FileUrl, tiffFilePath);
            }

            await tiffFileHelper.GetTiffMetadataAsync(req.SessionName, tiffFilePath, ct);
            response.TiffImages =
                await tiffFileHelper.UploadImagesByPageNumberAsync(tiffFilePath, new List<long>() { (long)pageNumber },
                    ct);
        
            await tiffFileHelper.DeleteOlderFiles(req.SessionName, ct);
        }

        await eventPublisher.PublishAsync(new PageChangedEvent()
        {
            SessionName = req.SessionName,
            PageNumber = (long)pageNumber
        }, ct);
        
        await SendAsync(response, cancellation: ct);
    }
}