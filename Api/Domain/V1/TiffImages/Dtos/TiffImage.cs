namespace Api.Domain.V1.TiffImages.Dtos;

public class TiffImage
{
    public long Offset { get; set; }
    public long Index {get; set;}
    public string Description { get; set; }
    public long PageNumber { get; set; }
    public string FilePath { get; set; }
    public string FileUrl { get; set; }
}