# Contract: IDocumentService

**Feature**: `001-document-management` | The single authority for document behaviour and access.

Every operation takes the **requesting user's id** and enforces authorization itself. No caller — page,
component, or controller — may decide access on its own (Constitution IV, FR-032, FR-033). This
mirrors the established `TaskService.GetTaskByIdAsync(taskId, requestingUserId)` shape already used
in this codebase.

## Authorization Rule

`CanAccessAsync(documentId, userId)` is the one place access is decided. It returns true when the
user is any of:

- the uploader,
- an Administrator,
- a member of the document's project,
- the Project Manager of that project,
- a Team Lead in the uploader's department,
- the target of an individual share,
- a member of a department the document is shared with.

Every other operation calls this first. Identity comes from claims; every other value is loaded from
the database, so no request-supplied field can grant permission (R-007).

## Upload

### `UploadDocumentAsync(request, userId) → Document`

Executes strictly in this order (R-004):

1. Validate type and size — reject with a specific reason (FR-002, FR-003).
2. Authorize the target project or task, if supplied (FR-033).
3. Scan via `IFileScanner`; abort if unsafe (FR-009).
4. Save the file via `IFileStorageService` and capture the returned path.
5. Insert the `Document` record.
6. Write an `Uploaded` activity record.
7. Notify project members, excluding the uploader (FR-028).

If step 5 fails, the file written in step 4 is deleted before the error surfaces. If step 4 fails, no
record is created. **No path produces a record without a file, or a file without a record** (FR-010).

Title defaults to the filename without its extension when left blank (FR-006).

## Retrieval

### `GetDocumentByIdAsync(documentId, userId) → Document?`

Returns `null` when the document does not exist **or** the user cannot access it. The two cases are
deliberately indistinguishable so the caller cannot probe for existence (R-005).

### `GetDocumentsAsync(userId, filter) → IEnumerable<Document>`

Returns only accessible documents, newest first. The access filter is applied **inside the database
query**, so inaccessible rows are never materialized (R-008, FR-012).

Filter supports category, project, uploader, date range, and a search term matching title,
description, tags, uploader name, and project name (FR-014 – FR-017).

### `GetProjectDocumentsAsync(projectId, userId)` / `GetTaskDocumentsAsync(taskId, userId)`

Scoped listings. Both verify access to the parent project or task before returning anything (FR-013).

### `GetDocumentStreamAsync(documentId, userId) → Stream?`

Authorizes, then opens content and records a `Downloaded` activity, incrementing `DownloadCount`.
Returns `null` on denial (FR-041).

## Modification

### `UpdateMetadataAsync(documentId, request, userId)`

Updates title, description, category, and tags only. Sets `LastModifiedAt`. Does **not** change
`Version` — version tracks content, not description (FR-020).

### `ReplaceContentAsync(documentId, stream, fileName, userId)`

Validates and scans the replacement, stores it, increments `Version`, updates size and type, and
writes a `ContentReplaced` activity. The previous file is deleted only after the record update
succeeds (FR-021).

### `DeleteDocumentAsync(documentId, userId) → bool`

Permanent. Removes shares, deletes the record, then deletes the file. Activity records are
**retained** so the audit trail survives (FR-022, FR-023, R-012). Only the uploader or an
Administrator may delete.

## Sharing

### `ShareWithUserAsync(documentId, targetUserId, userId)`

Grants individual access, writes a `Shared` activity, and raises a `DocumentShared` notification.
Requires the sharer to already have access (FR-030, FR-034, FR-035). Idempotent.

### `ShareWithDepartmentAsync(documentId, department, userId)`

Grants access to everyone in a department and notifies its members. Department is the unit of team
sharing per the FR-031 clarification (FR-031, FR-031a).

### `RevokeShareAsync(documentShareId, userId)`

Removes a grant and writes a `ShareRevoked` activity. Only the document owner or an Administrator may
revoke (FR-036).

### `GetSharesAsync(documentId, userId)`

Lists current grants. Owner and Administrator only.

## Activity

### `GetActivityAsync(documentId, userId)`

Returns the audit trail newest first, for users who can access the document (FR-040).

## Guarantees

- No operation returns or mutates a document the user cannot access.
- Denial is always indistinguishable from absence.
- Every state-changing operation writes exactly one activity record; failed operations write none.
- The file store and database never diverge.
