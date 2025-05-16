using System.Runtime.InteropServices;
using Api.Domain.V1.TiffImages.Dtos;
using FluentValidation;
using TiffLibrary;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TiffLibrary.PixelFormats;

namespace Api.Shared;

public interface ITiffFileHelper
{
    Task<List<TiffImage>> GetTiffMetadataAsync(string sessionName, string tiffFilePath,
        CancellationToken cancellationToken = default);

    Task<List<TiffImage>> GetJpgImagesAsync(string tiffFilePath, long pageNumber,
        CancellationToken cancellationToken = default);

    Task<List<TiffImage>> UploadImagesByPageNumberAsync(string tiffFilePath, List<long> pageNumber,
        CancellationToken ct = default);

    Task DeleteImagesByPageNumberAsync(List<long> pageNumber, CancellationToken ct = default);
    Task DeleteOlderFiles(string currentSessionName, CancellationToken ct = default);
}

public class TiffFileHelper(IMemoryCache memoryCache, IGoogleDriveService googleDriveService) : ITiffFileHelper
{
    public async Task<List<TiffImage>> GetTiffMetadataAsync(string sessionName, string tiffFilePath,
        CancellationToken cancellationToken = default)
    {
        var result = new List<TiffImage>();

        // Open the TIFF file
        await using var tiff = await TiffFileReader.OpenAsync(tiffFilePath, cancellationToken);

        long count = 0;
        long offset = tiff.FirstImageFileDirectoryOffset; // First Offset of Tiff File
        while (offset != 0)
        {
            var pageNumber = (((count + 1) - 1) / Constants.PageSize) + 1;
            var tiffImage = new TiffImage()
            {
                Offset = offset,
                Index = count + 1,
                Description = DateTime.Now.ToString("ddMMyyyy:HHmmss"),
                PageNumber = pageNumber,
                FilePath = Constants.JpgFilesPath + $"/{sessionName}_{pageNumber}_{count + 1}.jpg"
            };
            var cachedImages = new List<TiffImage>();
            if (memoryCache.TryGetValue(pageNumber, out cachedImages))
            {
                cachedImages.Add(tiffImage);
            }
            else
            {
                cachedImages = new List<TiffImage> { tiffImage };
            }

            memoryCache.Set(pageNumber, cachedImages);
            memoryCache.Set(Constants.TotalPagesCountMemoryCacheKey, pageNumber);    

            result.Add(tiffImage);
            offset = (await tiff.ReadImageFileDirectoryAsync(offset, cancellationToken)).NextOffset;
            count++;
        }

        return result;
    }

    public async Task<List<TiffImage>> GetJpgImagesAsync(string tiffFilePath, long pageNumber,
        CancellationToken cancellationToken = default)
    {
        memoryCache.TryGetValue(pageNumber, out List<TiffImage>? cachedImages);
        if (cachedImages == null)
        {
            throw new ValidationException("No images found for the specified page number.");
        }

        // Open the TIFF file
        await using var tiff = await TiffFileReader.OpenAsync(tiffFilePath, cancellationToken);

        foreach (var tiffImage in cachedImages)
        {
            if (File.Exists(tiffImage.FilePath))
            {
                continue;
            }

            var ifd = await tiff.ReadImageFileDirectoryAsync(tiffImage.Offset,
                cancellationToken); // Directly access to specific image in Tiff
            var frontDecoder = await tiff.CreateImageDecoderAsync(ifd, cancellationToken);
            await SaveTiffFrameAsJpegAsync(frontDecoder, tiffImage.FilePath);
        }

        return cachedImages;
    }

    public async Task<List<TiffImage>> UploadImagesByPageNumberAsync(string tiffFilePath, List<long> pages,
        CancellationToken ct = default)
    {
        var tiffImagesList = new List<TiffImage>();
        foreach (var pageNumber in pages)
        {
            var tiffImages = await GetJpgImagesAsync(tiffFilePath, (long)pageNumber, ct);

            var uploadTasks = tiffImages.Select(async tiffImage =>
            {
                if (!tiffImage.FileUrl.IsEmpty()) return;

                tiffImage.FileUrl = await googleDriveService.UploadFileAsync(tiffImage.FilePath);
                
                memoryCache.Set(pageNumber, tiffImages);
            });

            await Task.WhenAll(uploadTasks);
            tiffImagesList.AddRange(tiffImages);
        }

        return tiffImagesList;
    }

    public async Task DeleteImagesByPageNumberAsync(List<long> pagesToBeDeleted, CancellationToken ct = default)
    {
        var cachedImagesToBeDeleted = new List<TiffImage>();
        var uploadTasks = new List<Task>();

        foreach (var page in pagesToBeDeleted)
        {
            if (memoryCache.TryGetValue(page, out List<TiffImage>? cachedImages))
            {
                cachedImagesToBeDeleted.AddRange(cachedImages);
            }

            var tasks = cachedImagesToBeDeleted.Select(async tiffImage =>
            {
                if (!File.Exists(tiffImage.FilePath) || tiffImage.FileUrl.IsEmpty())
                {
                    return;
                }

                File.Delete(tiffImage.FilePath);
                await googleDriveService.DeleteFileAsync(tiffImage.FileUrl);
                tiffImage.FileUrl = string.Empty;
            }).ToList(); // Convert to List<Task>

            uploadTasks.AddRange(tasks);
            memoryCache.Set(page, cachedImagesToBeDeleted);
        }

        await Task.WhenAll(uploadTasks);
    }
    
    public async Task DeleteOlderFiles(string currentSessionName, CancellationToken ct = default)
    {
        // Remove all the files which are not starting with the current session name
        var files = Directory.GetFiles(Constants.JpgFilesPath, "*.jpg");
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!fileName.StartsWith(currentSessionName))
            {
                File.Delete(file);
            }
        }
        // Same for the tiff file
        var tiffFiles = Directory.GetFiles(Constants.TiffFileStoragePath, "*.tif");
        foreach (var file in tiffFiles)
        {
            var fileName = Path.GetFileName(file);
            if (!fileName.StartsWith(currentSessionName))
            {
                File.Delete(file);
            }
        }
        // Remove from Google Drive
        await googleDriveService.RemoveFilesNotStartingWithPrefixAsync(currentSessionName, null);
    }

    private static async Task SaveTiffFrameAsJpegAsync(TiffImageDecoder decoder, string outputPath)
    {
        // if path not exists, create them
        var directoryPath = Path.GetDirectoryName(outputPath);
        if (directoryPath != null && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var pixels = new TiffRgba32[decoder.Width * decoder.Height];
        var pixelBuffer = new TiffMemoryPixelBuffer<TiffRgba32>(pixels, decoder.Width, decoder.Height, writable: true);

        await decoder.DecodeAsync(pixelBuffer);

        using var image = Image.LoadPixelData<Rgba32>(MemoryMarshal.Cast<TiffRgba32, Rgba32>(pixels), decoder.Width,
            decoder.Height);

        await image.SaveAsJpegAsync(outputPath);
    }
}