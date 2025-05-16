using Api.Shared.Dtos;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using File = System.IO.File;

namespace Api.Shared;

public interface IGoogleDriveService
{
    Task<string> UploadFileAsync(string localFilePath);
    Task<string> DownloadFileAsync(string fileIdOrUrl, string destinationPath);
    Task DeleteFileAsync(string fileIdOrUrl);
    Task<string> GetFileIdFromUrlAsync(string url);
    Task<int> RemoveFilesNotStartingWithPrefixAsync(string prefix, string parentFolderId);
}

public class GoogleDriveService : IGoogleDriveService
{
    private readonly ILogger<GoogleDriveService> _logger;
    private readonly DriveService _driveService;
    private readonly GoogleDriveConfigs _settings;

    public GoogleDriveService(ILogger<GoogleDriveService> logger, IOptions<GoogleDriveConfigs> settings)
    {
        _logger = logger;
        _settings = settings.Value;

        GoogleCredential credential;
        using (var stream = new FileStream(_settings.CredentialsPath, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream).CreateScoped(DriveService.ScopeConstants.Drive);
        }

        _driveService = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = _settings.ApplicationName,
        });
    }

    public async Task<string> UploadFileAsync(string localFilePath)
    {
        try
        {
            _logger.LogInformation($"Uploading file: {localFilePath}");

            if (!File.Exists(localFilePath))
            {
                throw new FileNotFoundException($"File not found: {localFilePath}");
            }

            var fileName = Path.GetFileName(localFilePath);

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new List<string> { _settings.ParentFolderId }
            };

            var mimeType = GetMimeType(localFilePath);

            await using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);
            var request = _driveService.Files.Create(fileMetadata, stream, mimeType);
            request.Fields = "id, webViewLink, webContentLink";

            var uploadProgress = await request.UploadAsync();

            if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw new Exception($"Upload failed: {uploadProgress.Exception?.Message}");
            }

            var file = request.ResponseBody;
            await SetFilePublicAsync(file.Id);

            var downloadUrl = file.WebContentLink ?? $"https://drive.google.com/uc?export=download&id={file.Id}";
            _logger.LogInformation($"File uploaded successfully. ID: {file.Id}, URL: {downloadUrl}");

            return downloadUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error uploading file {localFilePath}");
            throw;
        }
    }

    public async Task<string> DownloadFileAsync(string fileIdOrUrl, string destinationPath)
    {
        try
        {
            string fileId = await GetFileIdFromUrlAsync(fileIdOrUrl);
            _logger.LogInformation($"Downloading file with ID: {fileId}");

            var request = _driveService.Files.Get(fileId);
            var memoryStream = new MemoryStream();

            await request.DownloadAsync(memoryStream);
            memoryStream.Position = 0;

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                await memoryStream.CopyToAsync(fileStream);
            }

            _logger.LogInformation(
                $"File downloaded successfully to {destinationPath}. Size: {memoryStream.Length} bytes");
            return destinationPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error downloading file {fileIdOrUrl}");
            throw;
        }
    }

    public async Task DeleteFileAsync(string fileIdOrUrl)
    {
        try
        {
            string fileId = await GetFileIdFromUrlAsync(fileIdOrUrl);
            _logger.LogInformation($"Deleting file with ID: {fileId}");

            await _driveService.Files.Delete(fileId).ExecuteAsync();
            _logger.LogInformation($"File deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting file {fileIdOrUrl}");
            throw;
        }
    }

    public async Task<string> GetFileIdFromUrlAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        if (!url.Contains("http") && !url.Contains("/") && !url.Contains("?"))
            return url;

        if (url.Contains("/d/"))
        {
            var parts = url.Split(new[] { "/d/" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                var idPart = parts[1];
                var idEndIndex = idPart.IndexOf("/");
                return idEndIndex > 0 ? idPart.Substring(0, idEndIndex) : idPart;
            }
        }
        else if (url.Contains("id="))
        {
            var parts = url.Split(new[] { "id=" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                var idPart = parts[1];
                var idEndIndex = idPart.IndexOf("&");
                return idEndIndex > 0 ? idPart.Substring(0, idEndIndex) : idPart;
            }
        }

        _logger.LogWarning($"Could not extract file ID from URL: {url}");
        throw new ArgumentException($"Could not extract file ID from URL: {url}");
    }

    public async Task<int> RemoveFilesNotStartingWithPrefixAsync(string prefix, string parentFolderId)
    {

        if (string.IsNullOrEmpty(parentFolderId))
        {
            parentFolderId = _settings.ParentFolderId;
        }

        try
        {
            _logger.LogInformation($"Removing files not starting with '{prefix}' from folder '{parentFolderId}'");

            // Create query to find all files in the specific folder
            var request = _driveService.Files.List();
            request.Q =
                $"'{parentFolderId}' in parents and trashed=false and mimeType != 'application/vnd.google-apps.folder'";
            request.Fields = "files(id, name)";
            request.Spaces = "drive";
            request.PageSize = 1000; // Adjust as needed

            // Execute the request
            var result = await request.ExecuteAsync();

            if (result.Files == null || result.Files.Count == 0)
            {
                _logger.LogInformation($"No files found in folder '{parentFolderId}'");
                return 0;
            }

            _logger.LogInformation($"Found {result.Files.Count} files in folder '{parentFolderId}'");

            // Filter files that don't start with the prefix
            var filesToDelete = result.Files
                .Where(file => !file.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogInformation($"Found {filesToDelete.Count} files not starting with '{prefix}' to delete");

            // Delete each file
            int deletedCount = 0;
            foreach (var file in filesToDelete)
            {
                try
                {
                    _logger.LogInformation($"Deleting file: {file.Name} (ID: {file.Id})");
                    await _driveService.Files.Delete(file.Id).ExecuteAsync();
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error deleting file {file.Name} (ID: {file.Id})");
                    // Continue with other files even if one fails
                }
            }

            _logger.LogInformation($"Successfully deleted {deletedCount} files not starting with '{prefix}'");
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing files not starting with '{prefix}' from folder '{parentFolderId}'");
            throw;
        }
    }


    private async Task<string> EnsureFolderExistsAsync(string folderPath)
    {
        var folders = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string parentFolderId = null;

        foreach (var folder in folders)
        {
            parentFolderId = await CreateFolderIfNotExistsAsync(folder, parentFolderId);
        }

        return parentFolderId;
    }

    private async Task<string> CreateFolderIfNotExistsAsync(string folderName, string parentFolderId)
    {
        var request = _driveService.Files.List();
        request.Q = parentFolderId == null
            ? $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and trashed=false"
            : $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and '{parentFolderId}' in parents and trashed=false";
        request.Fields = "files(id, name)";

        var result = await request.ExecuteAsync();
        var files = result.Files;

        if (files != null && files.Any())
        {
            return files.First().Id;
        }

        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = parentFolderId == null ? null : new List<string> { parentFolderId }
        };

        var createRequest = _driveService.Files.Create(fileMetadata);
        createRequest.Fields = "id";
        var file = await createRequest.ExecuteAsync();

        _logger.LogInformation($"Folder created: {folderName} with ID: {file.Id}");
        return file.Id;
    }

    private async Task SetFilePublicAsync(string fileId)
    {
        try
        {
            _logger.LogInformation($"Setting file {fileId} as publicly accessible");

            var permission = new Permission
            {
                Type = "anyone",
                Role = "reader"
            };

            await _driveService.Permissions.Create(permission, fileId).ExecuteAsync();
            _logger.LogInformation($"File {fileId} is now publicly accessible");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting file {fileId} as public");
            throw;
        }
    }

    private string GetMimeType(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".tif" or ".tiff" => "image/tiff",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }
}