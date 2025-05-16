namespace Api.Domain.V1.TiffImages.ListAllImages;

public class ListAllImagesRequestDto
{
    private string _sessionName;
    private string _fileUrl;

    public string SessionName
    {
        get => _sessionName;
        set => _sessionName = value?.Trim();
    }

    public string FileUrl
    {
        get => _fileUrl;
        set => _fileUrl = value?.Trim();
    }
    
    public long? PageNumber { get; set; } = 1;
    public long? Offset { get; set; }
    public long? Index { get; set; }
}