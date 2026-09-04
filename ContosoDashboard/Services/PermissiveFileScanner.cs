namespace ContosoDashboard.Services;

/// <summary>
/// Offline scanner stand-in. Approves every file that already passed type and size validation.
///
/// KNOWN LIMITATION: this detects nothing. It exists so the upload pipeline has a real scanning
/// stage that a genuine engine can be substituted into by changing one DI registration. The
/// application must run fully offline, so no real scanning engine is bundled (research R-002).
/// </summary>
public class PermissiveFileScanner : IFileScanner
{
    private readonly ILogger<PermissiveFileScanner> _logger;

    public PermissiveFileScanner(ILogger<PermissiveFileScanner> logger)
    {
        _logger = logger;
    }

    public Task<FileScanResult> ScanAsync(Stream content, string fileName)
    {
        // Restore the position so the caller can still persist the content.
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        _logger.LogDebug("Permissive scan approved {FileName} without inspection.", fileName);

        return Task.FromResult(FileScanResult.Safe());
    }
}
