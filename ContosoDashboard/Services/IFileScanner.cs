namespace ContosoDashboard.Services;

public interface IFileScanner
{
    /// <summary>
    /// Inspects content and reports whether it may be stored. Called after validation and before
    /// the file is written. Restores the stream position before returning.
    /// </summary>
    Task<FileScanResult> ScanAsync(Stream content, string fileName);
}

public class FileScanResult
{
    public bool IsSafe { get; init; }

    public string? Reason { get; init; }

    public static FileScanResult Safe() => new() { IsSafe = true };

    public static FileScanResult Unsafe(string reason) => new() { IsSafe = false, Reason = reason };
}
