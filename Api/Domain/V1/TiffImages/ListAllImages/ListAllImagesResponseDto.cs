using Api.Domain.V1.TiffImages.Dtos;
using Api.Shared.Dtos;

namespace Api.Domain.V1.TiffImages.ListAllImages;

public class ListAllImagesResponseDto : BaseSessionDetails
{
    public List<TiffImage> TiffImages { get; set; }
}