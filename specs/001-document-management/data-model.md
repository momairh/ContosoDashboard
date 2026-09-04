# Phase 1 Data Model: Document Upload and Management

**Feature**: `001-document-management` | **Date**: 2026-09-04

Three new entities join the existing model. Conventions follow the existing entities in
[ApplicationDbContext.cs](../../ContosoDashboard/Data/ApplicationDbContext.cs): `int` identity
primary keys, `[MaxLength]` on string columns, `virtual ICollection<>` navigation properties, and
relationship/index configuration in `OnModelCreating`.

---

## Entity: Document

Represents one uploaded file together with its metadata. One row per document; content lives on disk
(R-001), never in the database.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `DocumentId` | `int` | PK, identity | R-011 |
| `Title` | `string` | Required, MaxLength 200 | Defaults to filename without extension if the user leaves it blank (FR-006) |
| `Description` | `string?` | MaxLength 1000 | Optional (FR-006) |
| `Category` | `string` | Required, MaxLength 50 | One of six allowed values (R-010) |
| `Tags` | `string?` | MaxLength 500 | Normalized comma-delimited (R-009) |
| `OriginalFileName` | `string` | Required, MaxLength 255 | As supplied by the browser; display and download name only, never used to build a path (FR-011) |
| `StoredFileName` | `string` | Required, MaxLength 100 | `{guid}.{ext}` |
| `FilePath` | `string` | Required, MaxLength 500 | Relative path below the upload root; sized for the full `{userId}/{scope}/{guid}.{ext}` shape |
| `FileType` | `string` | Required, MaxLength 255 | MIME type. 255 because Office Open XML types are ~80 characters and easily overflow a smaller column |
| `FileSizeBytes` | `long` | Required, > 0, ≤ 26_214_400 | `long`, not `int` (FR-003) |
| `UploadedByUserId` | `int` | Required, FK → `User` | Owner (FR-007) |
| `ProjectId` | `int?` | FK → `Project` | Null means personal (FR-004) |
| `TaskId` | `int?` | FK → `ProjectTask` | Optional task association (FR-004) |
| `UploadedAt` | `DateTime` | Required, UTC | FR-007 |
| `LastModifiedAt` | `DateTime?` | UTC | Set on metadata edit or content replace (FR-020, FR-021) |
| `Version` | `int` | Required, default 1 | Incremented on content replace (FR-021) |
| `DownloadCount` | `int` | Required, default 0 | Incremented on each successful download (FR-041) |

**Navigation**: `UploadedBy` (User), `Project` (Project?), `Task` (ProjectTask?),
`Shares` (`ICollection<DocumentShare>`), `Activities` (`ICollection<DocumentActivity>`).

**Validation rules**

- `FileSizeBytes` must be greater than zero and at most 25 MB — enforced on the stream before the
  file is written, not after (FR-003, R-003).
- File extension must be in the whitelist; leading bytes must match for PDF, JPEG, PNG (FR-002, R-003).
- `Category` must be one of the six allowed values (FR-005).
- `ProjectId`, when supplied, must reference a project the uploader can access — validated
  server-side so a forged id cannot place a document into someone else's project (FR-033).
- `Version` starts at 1 and only ever increases.

**Relationships**

- `UploadedBy` → `User`, restrict delete. Deleting a user must not silently destroy their documents.
- `Project` → `Project`, optional, set null on delete. Documents survive project deletion as personal.
- `Task` → `ProjectTask`, optional, set null on delete.

**Indexes**

- `UploadedByUserId` — owner listing, the most common query.
- `ProjectId` — project document tab (FR-013).
- `TaskId` — task attachment lookup.
- `Category` — category filter (FR-015).
- `UploadedAt` — default newest-first sort (FR-012).
- Composite `(UploadedByUserId, UploadedAt)` — serves the owner listing and its sort in a single
  index, supporting the 2-second target (SC-002, R-008).

---

## Entity: DocumentShare

One row per grant. A share is always additive; it can never reduce access the recipient already has.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `DocumentShareId` | `int` | PK, identity | |
| `DocumentId` | `int` | Required, FK → `Document` | |
| `SharedWithUserId` | `int?` | FK → `User` | Set for an individual share (FR-030) |
| `SharedWithDepartment` | `string?` | MaxLength 100 | Set for a department share; matches `User.Department` length (FR-031) |
| `SharedByUserId` | `int` | Required, FK → `User` | Who granted it (FR-034) |
| `SharedAt` | `DateTime` | Required, UTC | |

**Navigation**: `Document`, `SharedWithUser` (User?), `SharedBy` (User).

**Validation rules**

- Exactly one of `SharedWithUserId` or `SharedWithDepartment` is set. Both null is a meaningless
  grant; both set is ambiguous.
- Only a user who can already access the document may share it (FR-035).
- A user cannot share a document with themselves.
- Re-sharing with the same target is idempotent — no duplicate row.

**Relationships**

- `Document` → cascade delete. Shares have no meaning once the document is gone (R-012).
- `SharedWithUser` → restrict delete.
- `SharedBy` → restrict delete.

**Indexes**

- `DocumentId` — list current shares.
- `SharedWithUserId` — "shared with me" listing.
- `SharedWithDepartment` — department-scoped access check.

---

## Entity: DocumentActivity

Append-only audit trail. Rows are never updated or deleted, including when the document they describe
is deleted (R-012).

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `DocumentActivityId` | `int` | PK, identity | |
| `DocumentId` | `int` | Required | **Not** a cascading FK — see below |
| `DocumentTitle` | `string` | Required, MaxLength 200 | Denormalized so the log stays readable after deletion (R-012) |
| `UserId` | `int` | Required, FK → `User` | Who acted (FR-039) |
| `ActivityType` | `DocumentActivityType` | Required | Enum |
| `Details` | `string?` | MaxLength 500 | Context, e.g. the share target or which fields changed |
| `OccurredAt` | `DateTime` | Required, UTC | FR-039 |

**Navigation**: `User`.

**Validation rules**

- Write-once. No update or delete path exists in the service layer.
- Every state-changing operation writes exactly one row (FR-039, FR-040).
- A failed operation writes no activity row — the log records what happened, not what was attempted.

**Relationships**

- `User` → restrict delete.
- `DocumentId` is stored as a plain column with a non-enforced relationship. A cascading foreign key
  would delete the audit trail along with the document, defeating FR-039. This is a deliberate
  denormalization and the reason `DocumentTitle` is duplicated here.

**Indexes**

- `DocumentId` — per-document history (FR-040).
- `UserId` — per-user activity.
- `OccurredAt` — chronological reporting (FR-041).

---

## Enum: DocumentActivityType

| Member | Written when |
|--------|--------------|
| `Uploaded` | A document is successfully created |
| `Downloaded` | Content is served for download (FR-041) |
| `Previewed` | Content is served inline |
| `MetadataUpdated` | Title, description, category, or tags change (FR-020) |
| `ContentReplaced` | The file is replaced and `Version` increments (FR-021) |
| `Shared` | A share is granted (FR-034) |
| `ShareRevoked` | A share is removed (FR-036) |
| `Deleted` | The document is permanently deleted (FR-023) |

---

## Existing Entities: Required Changes

### `NotificationType` enum — add two members

`DocumentShared` and `DocumentAdded`. Additive only; existing members keep their ordinal values so
seeded notification data is unaffected (R-013).

### `Notification` — no schema change

Reuses `Message`, `Link`, and `Type`. Document notifications link to the document detail page.

### `User` — no schema change

`Department` (nullable, MaxLength 100) is reused as the team identifier per the FR-031 clarification.
`DocumentShare.SharedWithDepartment` is deliberately sized to match.

### `Project` and `ProjectTask` — no schema change

Both gain an inverse `Documents` navigation collection only.

### `ApplicationDbContext` — add

- `DbSet<Document>`, `DbSet<DocumentShare>`, `DbSet<DocumentActivity>`.
- Relationship and delete-behaviour configuration for all three.
- The eleven indexes listed above.
- No seed data. Seeding documents would require seeding real files on disk; the quickstart validates
  by uploading instead.

### `Pages/Login.cshtml.cs` — add the Department claim

Lines 59-62 currently emit `NameIdentifier`, `Name`, `Email`, and `Role` only. Add a `Department`
claim when the user has one. **Without this, department-scoped sharing has nothing to match against
and silently denies access** (R-007, FR-031a).

---

## State Transitions

A document has no explicit status column; its lifecycle is implied by its fields.

```
(none) --upload--> Version 1, LastModifiedAt null
       --edit metadata--> LastModifiedAt set, Version unchanged
       --replace content--> Version + 1, LastModifiedAt set, new stored file
       --delete--> row and file removed; activity rows retained
```

Editing metadata deliberately does not bump `Version` — version tracks content, not description
(FR-020 versus FR-021).

---

## Entity Relationship Summary

```
User 1 ──< Document           (UploadedByUserId, restrict)
User 1 ──< DocumentShare      (SharedWithUserId, restrict)
User 1 ──< DocumentShare      (SharedByUserId, restrict)
User 1 ──< DocumentActivity   (UserId, restrict)

Project 0..1 ──< Document     (ProjectId, set null)
ProjectTask 0..1 ──< Document (TaskId, set null)

Document 1 ──< DocumentShare  (cascade)
Document 1 ─ ─< DocumentActivity  (no enforced FK, intentional)
```

---

## Requirements Traceability

| Entity / change | Satisfies |
|-----------------|-----------|
| `Document` core fields | FR-001 – FR-008, FR-011 |
| `Document.ProjectId` / `TaskId` | FR-004, FR-013 |
| `Document.Version`, `LastModifiedAt` | FR-020, FR-021 |
| `Document.DownloadCount` | FR-041 |
| Indexes | SC-002, SC-004 |
| `DocumentShare` individual | FR-030, FR-034 – FR-036 |
| `DocumentShare` department | FR-031, FR-031a |
| `DocumentActivity` | FR-039, FR-040 |
| Retained activity on delete | FR-022, FR-023, FR-039 |
| `NotificationType` additions | FR-028, FR-030 |
| Department claim | FR-031a, FR-032 |
