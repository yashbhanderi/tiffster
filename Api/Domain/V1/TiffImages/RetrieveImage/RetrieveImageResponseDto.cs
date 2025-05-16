using Api.Shared.Dtos;

namespace Api.Domain.V1.TiffImages.ListAllImages;

public class RetrieveResponseDto : BaseSessionDetails
{
    public Guid Id { get; set; }
}