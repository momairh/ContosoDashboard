using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class Document
{
    [Key]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = DocumentCategory.Other;

    // Normalized comma-delimited values; see research R-009.
    [MaxLength(500)]
    public string? Tags { get; set; }

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    // 255 because Office Open XML MIME types are long enough to overflow smaller columns.
    [Required]
    [MaxLength(255)]
    public string FileType { get; set; } = string.Empty;

    [Required]
    public long FileSizeBytes { get; set; }

    [Required]
    public int UploadedByUserId { get; set; }

    public int? ProjectId { get; set; }

    public int? TaskId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastModifiedAt { get; set; }

    public int Version { get; set; } = 1;

    public int DownloadCount { get; set; }

    // Navigation properties
    public virtual User? UploadedBy { get; set; }
    public virtual Project? Project { get; set; }
    public virtual TaskItem? Task { get; set; }
    public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();

    // No Activities navigation: DocumentActivity rows deliberately outlive the document they
    // describe, so no foreign key may link them (research R-012). Activity is queried by
    // DocumentId directly.
}
