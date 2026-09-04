using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileScanner _fileScanner;
    private readonly DocumentStorageOptions _options;
    private readonly ILogger<DocumentService> _logger;

    // Signatures for formats with stable, unambiguous magic numbers. Office formats are ZIP
    // containers and plain text has no signature, so those are validated by extension only
    // (research R-003).
    private static readonly Dictionary<string, byte[][]> FileSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } }
    };

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        IFileScanner fileScanner,
        IOptions<DocumentStorageOptions> options,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _fileScanner = fileScanner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DocumentOperationResult> UploadDocumentAsync(DocumentUploadRequest request, int requestingUserId)
    {
        // Step 1: validate type and size before anything is written.
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return DocumentOperationResult.Failure(validationError);
        }

        if (!await IsSignatureValidAsync(request))
        {
            return DocumentOperationResult.Failure(
                "The file content does not match its extension and was rejected.");
        }

        // Step 2: authorize the target project, so a forged id cannot place a document into
        // someone else's project (FR-033).
        if (request.ProjectId.HasValue &&
            !await IsProjectMemberAsync(request.ProjectId.Value, requestingUserId))
        {
            return DocumentOperationResult.Failure(
                "You do not have access to the selected project.");
        }

        // Step 3: scan.
        var scanResult = await _fileScanner.ScanAsync(request.Content, request.OriginalFileName);
        if (!scanResult.IsSafe)
        {
            return DocumentOperationResult.Failure(
                scanResult.Reason ?? "The file was rejected by the content safety check.");
        }

        // Step 4: write the file and capture the path.
        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        string relativePath;
        try
        {
            relativePath = await _fileStorage.SaveFileAsync(
                request.Content, request.OriginalFileName, requestingUserId, request.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store uploaded file {FileName}", request.OriginalFileName);
            return DocumentOperationResult.Failure("The file could not be saved. Please try again.");
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? Path.GetFileNameWithoutExtension(request.OriginalFileName)
            : request.Title.Trim();

        var document = new Document
        {
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = request.Category,
            Tags = NormalizeTags(request.Tags),
            OriginalFileName = request.OriginalFileName,
            StoredFileName = Path.GetFileName(relativePath),
            FilePath = relativePath,
            FileType = string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            UploadedByUserId = requestingUserId,
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            UploadedAt = DateTime.UtcNow,
            Version = 1
        };

        // Steps 5 and 6: insert the record and its activity. If this fails, the file written in
        // step 4 is removed so no orphaned file can survive (FR-010).
        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            _context.DocumentActivities.Add(new DocumentActivity
            {
                DocumentId = document.DocumentId,
                DocumentTitle = document.Title,
                UserId = requestingUserId,
                ActivityType = DocumentActivityType.Uploaded,
                OccurredAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist document record; removing orphaned file {Path}", relativePath);
            await _fileStorage.DeleteFileAsync(relativePath);
            return DocumentOperationResult.Failure("The document could not be saved. Please try again.");
        }

        return DocumentOperationResult.Success(document);
    }

    public async Task<List<Document>> GetDocumentsAsync(int requestingUserId, DocumentFilter? filter = null)
    {
        filter ??= new DocumentFilter();

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == requestingUserId);

        if (user is null)
        {
            return new List<Document>();
        }

        // The access filter is applied inside the query, so inaccessible rows are never
        // materialized (research R-008, FR-017).
        var query = _context.Documents
            .Include(d => d.UploadedBy)
            .Include(d => d.Project)
            .AsNoTracking()
            .Where(BuildAccessPredicate(user));

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(d => d.Category == filter.Category);
        }

        if (filter.ProjectId.HasValue)
        {
            query = query.Where(d => d.ProjectId == filter.ProjectId.Value);
        }

        if (filter.UploadedByUserId.HasValue)
        {
            query = query.Where(d => d.UploadedByUserId == filter.UploadedByUserId.Value);
        }

        if (filter.UploadedFrom.HasValue)
        {
            query = query.Where(d => d.UploadedAt >= filter.UploadedFrom.Value);
        }

        if (filter.UploadedTo.HasValue)
        {
            query = query.Where(d => d.UploadedAt <= filter.UploadedTo.Value);
        }

        query = filter.SortOrder switch
        {
            DocumentSortOrder.OldestFirst => query.OrderBy(d => d.UploadedAt),
            DocumentSortOrder.TitleAscending => query.OrderBy(d => d.Title),
            _ => query.OrderByDescending(d => d.UploadedAt)
        };

        return await query.ToListAsync();
    }

    public async Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId)
    {
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == requestingUserId);

        if (user is null)
        {
            return null;
        }

        // Returns null indistinguishably for missing and inaccessible documents, so a caller
        // cannot probe for existence (research R-005).
        return await _context.Documents
            .Include(d => d.UploadedBy)
            .Include(d => d.Project)
            .AsNoTracking()
            .Where(BuildAccessPredicate(user))
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);
    }

    public async Task<bool> CanAccessAsync(int documentId, int requestingUserId)
    {
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == requestingUserId);

        if (user is null)
        {
            return false;
        }

        return await _context.Documents
            .AsNoTracking()
            .Where(BuildAccessPredicate(user))
            .AnyAsync(d => d.DocumentId == documentId);
    }

    /// <summary>
    /// The single authorization rule. Every listing and lookup path composes this same
    /// predicate, so no path can drift from another (research R-007).
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<Document, bool>> BuildAccessPredicate(User user)
    {
        var userId = user.UserId;
        var isAdministrator = user.Role == UserRole.Administrator;
        var isTeamLead = user.Role == UserRole.TeamLead;
        var department = user.Department;

        return d =>
            isAdministrator
            || d.UploadedByUserId == userId
            || (d.ProjectId != null && d.Project != null &&
                (d.Project.ProjectManagerId == userId ||
                 d.Project.ProjectMembers.Any(m => m.UserId == userId)))
            || (isTeamLead && department != null && d.UploadedBy != null &&
                d.UploadedBy.Department == department)
            || d.Shares.Any(s => s.SharedWithUserId == userId)
            || (department != null && d.Shares.Any(s => s.SharedWithDepartment == department));
    }

    private string? ValidateRequest(DocumentUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            return "Select a file to upload.";
        }

        if (request.FileSizeBytes <= 0)
        {
            return "The selected file is empty.";
        }

        if (request.FileSizeBytes > _options.MaxFileSizeBytes)
        {
            var limitMb = _options.MaxFileSizeBytes / 1024d / 1024d;
            return $"The file exceeds the {limitMb:0} MB limit.";
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return $"Files of type '{extension}' are not supported.";
        }

        if (!DocumentCategory.IsValid(request.Category))
        {
            return "Select a valid category.";
        }

        return null;
    }

    /// <summary>
    /// Verifies leading bytes for formats with reliable signatures. Partially answers the
    /// "executable renamed to .pdf" case without a scanning engine.
    /// </summary>
    private static async Task<bool> IsSignatureValidAsync(DocumentUploadRequest request)
    {
        var extension = Path.GetExtension(request.OriginalFileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !FileSignatures.TryGetValue(extension, out var signatures))
        {
            return true;
        }

        if (!request.Content.CanSeek)
        {
            return true;
        }

        var longest = signatures.Max(s => s.Length);
        var header = new byte[longest];

        request.Content.Position = 0;
        var read = await request.Content.ReadAsync(header.AsMemory(0, longest));
        request.Content.Position = 0;

        if (read < longest)
        {
            return false;
        }

        return signatures.Any(signature => header.Take(signature.Length).SequenceEqual(signature));
    }

    private async Task<bool> IsProjectMemberAsync(int projectId, int userId)
    {
        return await _context.Projects
            .AsNoTracking()
            .AnyAsync(p => p.ProjectId == projectId &&
                           (p.ProjectManagerId == userId ||
                            p.ProjectMembers.Any(m => m.UserId == userId)));
    }

    /// <summary>Trimmed, lowercased, de-duplicated, comma-delimited (research R-009).</summary>
    private static string? NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return null;
        }

        var normalized = tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();

        return normalized.Count == 0 ? null : string.Join(',', normalized);
    }
}
