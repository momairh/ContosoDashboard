using ContosoDashboard.Models;
using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IOptions<DocumentStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.RootPath));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveFileAsync(Stream content, string originalFileName, int userId, int? projectId)
    {
        // The stored name is a GUID, so a hostile or duplicate original filename cannot influence
        // the path or collide with another user's file (FR-011).
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var scope = projectId.HasValue ? projectId.Value.ToString() : "personal";
        var relativePath = Path.Combine(userId.ToString(), scope, storedFileName)
            .Replace(Path.DirectorySeparatorChar, '/');

        var absolutePath = ResolveWithinRoot(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var target = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write))
        {
            await content.CopyToAsync(target);
        }

        return relativePath;
    }

    public Task<Stream> GetFileAsync(string relativePath)
    {
        var absolutePath = ResolveWithinRoot(relativePath);

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Stored file not found.", relativePath);
        }

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task<bool> DeleteFileAsync(string relativePath)
    {
        try
        {
            var absolutePath = ResolveWithinRoot(relativePath);

            if (!File.Exists(absolutePath))
            {
                return Task.FromResult(false);
            }

            File.Delete(absolutePath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete stored file {RelativePath}", relativePath);
            return Task.FromResult(false);
        }
    }

    public Task<bool> FileExistsAsync(string relativePath)
    {
        try
        {
            return Task.FromResult(File.Exists(ResolveWithinRoot(relativePath)));
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Resolves a relative path and confirms it stays below the storage root. Defends against
    /// traversal even if a malformed value reaches this layer.
    /// </summary>
    private string ResolveWithinRoot(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Storage path must not be empty.");
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Storage path must be relative.");
        }

        var combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected storage path escaping the root: {RelativePath}", relativePath);
            throw new InvalidOperationException("Storage path resolves outside the storage root.");
        }

        return combined;
    }
}
