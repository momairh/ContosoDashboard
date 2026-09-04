using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentShare
{
    [Key]
    public int DocumentShareId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    // Exactly one of SharedWithUserId or SharedWithDepartment is set.
    public int? SharedWithUserId { get; set; }

    // Sized to match User.Department, which is the unit of team sharing (FR-031).
    [MaxLength(100)]
    public string? SharedWithDepartment { get; set; }

    [Required]
    public int SharedByUserId { get; set; }

    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Document? Document { get; set; }
    public virtual User? SharedWithUser { get; set; }
    public virtual User? SharedBy { get; set; }
}
