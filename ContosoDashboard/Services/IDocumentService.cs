using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

/// <summary>
/// Single authority for document behaviour and access. Every operation takes the requesting
/// user's id and enforces authorization itself — no page or controller may decide access
/// on its own (FR-032, FR-033).
/// </summary>
public interface IDocumentService
{
    Task<DocumentOperationResult> UploadDocumentAsync(DocumentUploadRequest request, int requestingUserId);

    Task<List<Document>> GetDocumentsAsync(int requestingUserId, DocumentFilter? filter = null);

    Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId);

    Task<bool> CanAccessAsync(int documentId, int requestingUserId);
}
