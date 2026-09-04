namespace ContosoDashboard.Models;

public class DocumentFilter
{
    public string? SearchTerm { get; set; }

    public string? Category { get; set; }

    public int? ProjectId { get; set; }

    public int? UploadedByUserId { get; set; }

    public DateTime? UploadedFrom { get; set; }

    public DateTime? UploadedTo { get; set; }

    public DocumentSortOrder SortOrder { get; set; } = DocumentSortOrder.NewestFirst;
}

public enum DocumentSortOrder
{
    NewestFirst,
    OldestFirst,
    TitleAscending
}
