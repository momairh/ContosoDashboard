using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

/// <summary>
/// Append-only audit record. Rows are retained after the document they describe is deleted,
/// which is why the title is denormalized here (research R-012).
/// </summary>
public class DocumentActivity
{
    [Key]
    public int DocumentActivityId { get; set; }

    // Deliberately not an enforced foreign key: a cascade would destroy the audit trail.
    [Required]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(200)]
    public string DocumentTitle { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }

    [Required]
    public DocumentActivityType ActivityType { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User? User { get; set; }
}
