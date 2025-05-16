namespace Api.Shared;

public class Constants
{
    public const string ResponseHeadersTokenKey= "X-Token";
    
    public const string TiffFileStoragePath = @"D:\New folder\Tiffster\Api\Domain\V1\TiffImages\Images\TIFF";
    public const string JpgFilesPath = @"D:\New folder\Tiffster\Api\Domain\V1\TiffImages\Images\JPG";
    public const long PreRenderPageWindow = 1; // Number of pages to pre-render
    
    public const string CurrentPageMemoryCacheKey = "CurrentPage";
    public const string CurrentWindowMemoryCacheKey = "CurrentWindow";
    public const string TotalPagesCountMemoryCacheKey = "TotalPagesCount";

    public const int WindowSize = 3;
    public const int PageSize = 3;
    
    public const int RetryIntervalBaseInSecond = 2;
    public const int RetryCount = 3;
}