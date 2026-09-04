namespace ContosoDashboard.Services;

/// <summary>
/// Storage abstraction. Deliberately names no drive, directory, or provider so the local
/// implementation can be replaced by cloud storage without touching DocumentService.
/// Performs no authorization — access is decided by DocumentService before these are reached.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Writes content and returns the relative path to persist. The original filename never
    /// influences the stored path (FR-011).
    /// </summary>
    Task<string> SaveFileAsync(Stream content, string originalFileName, int userId, int? projectId);

    Task<Stream> GetFileAsync(string relativePath);

    /// <summary>Returns false rather than throwing when the file is already gone.</summary>
    Task<bool> DeleteFileAsync(string relativePath);

    Task<bool> FileExistsAsync(string relativePath);
}
