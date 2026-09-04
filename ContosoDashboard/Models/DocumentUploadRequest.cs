namespace ContosoDashboard.Models;

public class DocumentUploadRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string Category { get; set; } = DocumentCategory.Other;

    public string? Tags { get; set; }

    public int? ProjectId { get; set; }

    public int? TaskId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Buffered content. Buffering decouples the browser stream lifetime from the service call
    /// (research R-006).
    /// </summary>
    public Stream Content { get; set; } = Stream.Null;
}

public class DocumentOperationResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public Document? Document { get; init; }

    public static DocumentOperationResult Success(Document document) =>
        new() { Succeeded = true, Document = document };

    public static DocumentOperationResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
