# Contract: IFileStorageService

**Feature**: `001-document-management` | Abstracts physical file storage away from document logic.

The interface is deliberately storage-agnostic. Nothing in it names a drive, directory, or provider,
so the local implementation can be replaced by cloud storage without touching `DocumentService`
(Constitution IV, R-001).

## Operations

### `SaveFileAsync(stream, originalFileName, userId, projectId) → string`

Writes content to storage and returns the **relative** path to persist.

- Generates a GUID-based stored filename; the original name never influences the path (FR-011).
- Places the file at `{userId}/{projectId|"personal"}/{guid}.{ext}`.
- Creates missing directories.
- The returned path is relative to the storage root — never absolute, so the database stays portable.
- Throws if the write fails. Callers must assume no file exists on failure.

### `GetFileAsync(relativePath) → Stream`

Opens content for reading.

- Read-only stream; the caller disposes it.
- Throws `FileNotFoundException` if the path resolves to nothing.
- **Performs no authorization.** Access is decided by `DocumentService` before this is reached
  (R-005).

### `DeleteFileAsync(relativePath) → bool`

Removes content. Returns `false` rather than throwing when the file is already gone, so cleanup and
compensating deletes are safely repeatable (R-004, R-012).

### `FileExistsAsync(relativePath) → bool`

Checks presence without opening the file. Used to detect records whose file has disappeared (FR-024).

## Guarantees

- Every path is resolved and confirmed to sit **below the configured storage root**. A path escaping
  it is rejected, defending against traversal even if a malformed value reaches this layer.
- The storage root sits outside `wwwroot`, so no file is reachable by URL (FR-032).
- The service holds no knowledge of documents, users, or permissions — only bytes and paths.

## Constraints

- No caller may pass a user-supplied filename as `relativePath`.
- The implementation neither enforces nor knows about the size limit; that is validated upstream
  before the stream arrives (R-003).
