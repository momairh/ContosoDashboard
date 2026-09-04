# Phase 0 Research: Document Upload and Management

**Feature**: `001-document-management` | **Date**: 2026-09-04

This document resolves the open technical decisions required before design. Each entry records the
decision, why it was chosen, and what was rejected. Several decisions were pre-constrained by the
stakeholder requirements document or by the project constitution; those constraints are cited.

---

## R-001: File content storage location and layout

**Decision**: Store file content on the local filesystem under
`ContosoDashboard/AppData/uploads/{userId}/{projectId|"personal"}/{guid}.{ext}`, outside `wwwroot`.
Persist only the relative path in the database. Create the directory tree on demand at startup and
on first write.

**Rationale**:

- Storing outside `wwwroot` means no upload is reachable by URL without passing through application
  code, which is what makes the authorization check unavoidable rather than optional
  (Constitution III, FR-032).
- A GUID filename removes any influence of the user-supplied name on the stored path, eliminating
  path traversal and collision entirely (FR-011, and the edge cases covering hostile and duplicate
  filenames).
- The `{userId}/{projectId}/{guid}.{ext}` shape is also a valid Azure Blob name, so the same stored
  value works after migration with no schema change.
- Relative paths keep the database portable between machines and OSes.

**Alternatives considered**:

- *Store bytes in the database as BLOBs*: Removes orphaned-file risk and makes save atomic, but
  bloats the SQLite file, loads whole files into memory to serve them, and abandons the
  `IFileStorageService` teaching point the stakeholder document explicitly requires.
- *Store under `wwwroot/uploads`*: Simplest to serve, but makes every uploaded file publicly
  retrievable by anyone who guesses or obtains the URL. Directly violates Constitution III and FR-032.
- *Preserve the original filename on disk*: Friendlier for debugging, but reintroduces traversal
  risk, collisions between users, and invalid-character failures.

---

## R-002: Malware and content safety in an offline application

**Decision**: Define `IFileScanner` with a single `ScanAsync` operation returning an allow/deny
result plus a reason. Register `PermissiveFileScanner` (approves everything already passing type and
size validation) as the default. Call the scanner in `DocumentService` between validation and
persistence. Record the absence of real scanning as a Known Limitation in the README.

**Rationale**:

- Resolves the direct conflict between the stakeholder requirement for virus/malware scanning and
  Constitution II's prohibition on mandatory external dependencies. Confirmed with the user during
  clarification.
- The scan step exists in the real upload pipeline, so substituting a genuine scanner later requires
  changing one DI registration and nothing else.
- Mirrors how the project already handles storage and authentication: a real seam with an honest
  local stand-in, documented rather than hidden.

**Alternatives considered**:

- *No scanning concept at all*: Fewer moving parts, but leaves no insertion point and teaches that
  upload pipelines have no safety stage.
- *Bundle a real scanning engine*: Would satisfy the requirement literally, but adds a large binary
  dependency, breaks the "runs on a fresh offline machine" guarantee, and is disproportionate for a
  training app.
- *Queue-based asynchronous scanning in the cloud*: The correct production answer, recorded as
  **R-014**. Out of scope here because it reintroduces the cloud dependency this decision exists to
  avoid, and changes scanning from preventive to detective.
- *Validate magic-number/file signature as a pseudo-scan*: Rejected as the primary answer because it
  conflates type validation with malware detection, but adopted as a defence-in-depth extra in R-003.

---

## R-014: Asynchronous scanning architecture (future / production)

**Decision**: Document an Azure Functions + Queue Storage design as the intended **production**
implementation behind `IFileScanner`. It is **not built in this feature.** The offline
`PermissiveFileScanner` from R-002 remains the default and only registered implementation.

**Rationale**:

- Real malware scanning is slow and variable — seconds to minutes for large files. Running it inline
  during upload, as R-002's seam does, would hold the HTTP request open and breach the 30-second
  upload target (SC-001). Queue-triggered processing moves that cost off the request path.
- Recording it now gives the `IFileScanner` seam a concrete destination, so the abstraction is
  visibly load-bearing rather than speculative.
- Deferring the build keeps Constitution II intact: the application still runs on a disconnected
  ARM64 machine with no cloud account.

**Target architecture** (for reference only):

1. Upload completes validation and storage as in R-004, and the document is persisted with a
   `ScanStatus` of *Pending*.
2. `DocumentService` enqueues a scan message — document id and stored path — to Queue Storage.
3. A queue-triggered Azure Function reads the message, retrieves the blob, and submits it to a
   scanning engine.
4. The Function writes the verdict back, moving `ScanStatus` to *Clean* or *Infected*.
5. Infected files are deleted or quarantined, the uploader is notified, and the outcome is written to
   `DocumentActivity`.
6. Poison messages fall to a dead-letter queue after the configured retry count and are surfaced for
   manual review rather than silently dropped.

**Behavioural consequences if adopted** — these are the reason this is not a drop-in change:

- Scanning becomes **detective rather than preventive**. A file is stored and visible before its
  verdict arrives, so FR-009's "block on unsafe" no longer holds as written and would need
  rewording, along with a decision on whether *Pending* documents are downloadable.
- `Document` gains a `ScanStatus` column and the UI gains a pending state — a data-model and
  spec change, not merely a new `IFileScanner` registration.
- Introduces Azure Storage, an Azure Functions host, and a scanning engine as hard runtime
  dependencies, plus blob storage in place of `LocalFileStorageService` for the Function to reach
  the content.

**Alternatives considered**:

- *Build it now as the primary design*: Rejected by the user during clarification. It breaks
  Principle II and expands scope well beyond a training exercise.
- *Inline call to a hosted scanning API*: Simpler, no queue, and preserves the preventive block —
  but couples upload latency to a third-party service and still requires network access.
- *Local background worker (`IHostedService`) with an in-process queue*: Would give asynchronous
  scanning while staying offline. Genuinely viable, and the closer stepping stone — but it adds
  background-processing machinery and the same `ScanStatus` complexity for a scanner that, per R-002,
  detects nothing. Worth revisiting only alongside a real engine.

---

## R-003: File type and size validation strategy

**Decision**: Validate in three layers: (1) constrain the browser picker via the `accept` attribute,
(2) enforce an extension whitelist and the 25 MB limit server-side in `DocumentService` before any
write, (3) verify the leading file-signature bytes for PDF, JPEG, and PNG. Reject with a specific,
user-facing reason. Enforce the size cap on the read stream so an oversized upload is abandoned
rather than buffered whole.

**Rationale**:

- Client-side `accept` is a convenience, never a control; the server-side check is authoritative
  (FR-002, FR-003).
- Signature checking partially answers the "executable renamed to `.pdf`" edge case without a
  scanning engine.
- Enforcing the cap while streaming avoids a trivial memory-exhaustion vector from a 25 MB+ upload.
- Office formats are ZIP containers, so signature checks are applied only to formats with stable,
  unambiguous magic numbers rather than pretending to validate all types.

**Alternatives considered**:

- *MIME type from the browser only*: Trivially spoofed by the client; unsuitable as the sole gate.
- *Signature validation for every supported type*: Fragile for legacy Office binary formats and
  plain text, and would reject valid files — a poor trade in a training app.

---

## R-004: Upload sequencing to prevent orphaned records

**Decision**: Order the upload as: validate → authorize → scan → generate GUID path → write file to
disk → insert database record → write activity record → send notifications. If the database insert
fails, delete the just-written file before surfacing the error. If the file write fails, no database
record is ever created.

**Rationale**:

- Directly implements FR-010 and SC-007, and follows the sequencing the stakeholder document calls
  out as the fix for duplicate-key and orphaned-record failures.
- Generating the path before insert means the record is never written with an empty or placeholder
  path, which is the specific failure the stakeholder document reports having hit.
- Compensating deletion on insert failure keeps the two stores consistent without needing a
  distributed transaction, which SQLite plus filesystem cannot provide anyway.

**Alternatives considered**:

- *Insert the database row first, then write the file*: Produces a listed-but-unretrievable document
  whenever the write fails — the exact defect FR-010 forbids.
- *Wrap both in a transaction*: Not achievable across filesystem and database; a transaction cannot
  roll back a completed file write.
- *Background reconciliation sweep*: Adds a scheduled process and hidden state for a problem that
  correct ordering already solves.

---

## R-005: Serving downloads and previews

**Decision**: Add `DocumentsController`, an MVC controller carrying `[Authorize]`, exposing download
and inline-preview endpoints keyed by document id. The controller resolves the caller from claims,
delegates the authorization decision to `DocumentService`, and returns `NotFound` — not `Forbid` —
when access is denied. Enable controller endpoint routing in `Program.cs`.

**Rationale**:

- Blazor Server components render markup over a SignalR circuit and cannot return an HTTP file
  response, so a controller endpoint is the supported mechanism.
- Returning `NotFound` for both "missing" and "not yours" avoids confirming that a document id
  exists, closing an enumeration side channel (FR-032).
- Keeping the authorization decision inside `DocumentService` rather than the controller preserves
  Constitution IV and means the same rule governs UI listing and direct URL access.
- Inline preview is achieved with a `Content-Disposition: inline` response for PDF, JPEG, and PNG
  only; all other types force download (FR-018).

**Alternatives considered**:

- *Stream bytes through the Blazor circuit as a base64 data URL*: Works for small images, but blows
  up memory and circuit bandwidth for 25 MB files and breaks browser-native PDF viewing.
- *Static file middleware over the upload directory*: Would make every document world-readable.
  Rejected outright under Constitution III.
- *Return `Forbid` on denial*: Leaks existence of the document id.

---

## R-006: Blazor file upload component handling

**Decision**: Use `InputFile` with a `@key` that changes after each successful upload. Read
`Name`, `Size`, and `ContentType` into locals before opening the stream, copy
`OpenReadStream(maxAllowedSize)` into a `MemoryStream`, reset position, then release the
`IBrowserFile` reference and call `StateHasChanged()`.

**Rationale**:

- Follows the pattern the stakeholder document specifies, which exists because `IBrowserFile` streams
  are single-use and are disposed when the component re-renders — a real, previously-encountered
  failure in this codebase.
- Buffering to `MemoryStream` decouples the browser stream lifetime from the service call, so the
  service can retry or validate without a disposed-stream exception.
- Rotating `@key` forces a fresh `InputFile`, preventing the "same file cannot be re-selected" and
  stale-reference errors.
- Memory cost is bounded by the 25 MB cap enforced before buffering.

**Alternatives considered**:

- *Pass `IBrowserFile` directly into the service*: Leaks a UI-framework type into the service layer
  (violating Constitution IV) and reintroduces the disposal bug.
- *Stream straight to disk without buffering*: Lower memory use, but the file must be written before
  validation and scanning complete, which conflicts with R-004's ordering.

---

## R-007: Authorization model and the missing Department claim

**Decision**: Resolve access in `DocumentService` via a single `CanAccessAsync(documentId, userId)`
rule: allow if the user is the uploader, an Administrator, a member of the associated project, the
Project Manager of that project, a Team Lead in the uploader's department, or the target of an
individual or department share. **Add the missing `Department` claim to the login flow.**

**Rationale**:

- Centralizing the rule in one method means listing, search, download, preview, edit, replace,
  delete, and share all consult identical logic — no path can drift (FR-032, FR-033).
- Investigation of [Login.cshtml.cs](../../ContosoDashboard/Pages/Login.cshtml.cs) shows the cookie
  currently carries only `NameIdentifier`, `Name`, `Email`, and `Role`. There is **no `Department`
  claim**, yet the clarified FR-031 makes department the unit of team sharing. Without this fix,
  department-scoped authorization silently fails. The stakeholder document flags this exact hazard.
- Authorization derives user identity from claims and every other input from the database, so no
  request-supplied value can grant permission.

**Alternatives considered**:

- *Read `Department` from the database on every check*: Avoids the claim change, but adds a query to
  every authorization call and diverges from how `Role` is already handled.
- *Per-page authorization logic*: Guarantees divergence between paths and violates Constitution IV.

---

## R-008: Search implementation within SQLite

**Decision**: Implement search as a case-insensitive `LIKE`-style contains match over title,
description, tags, uploader display name, and project name, composed in LINQ and translated to SQL by
EF Core. Apply the access filter as part of the same query so unauthorized rows are excluded in the
database, not in memory. Index the columns used for filtering and sorting.

**Rationale**:

- SQLite is available offline and needs no search service, satisfying Constitution II.
- Filtering before materialization means an unauthorized document is never loaded, which is both
  faster and safer than post-filtering a result set (FR-017).
- At training scale (hundreds of documents), indexed `LIKE` comfortably meets the 2-second target
  (SC-004).

**Alternatives considered**:

- *SQLite FTS5 full-text index*: Faster at scale and supports ranking, but adds schema complexity and
  a separate index to maintain, for a corpus small enough not to need it. Full-text search of document
  *contents* is explicitly out of scope.
- *Load all accessible documents and filter in C#*: Simple, but reads the whole table on every search
  and risks materializing rows the user may not access.

---

## R-009: Tags representation

**Decision**: Store tags as a single delimited string column on `Document`, normalized on save
(trimmed, lowercased, de-duplicated, comma-separated with no spaces around delimiters).

**Rationale**:

- The spec treats tags as free-text with no controlled vocabulary, no tag management UI, and no
  browse-by-tag requirement, so a join table would add a table, a relationship, and query complexity
  for capability nobody asked for — a poor trade under Constitution I.
- Contains-matching over the delimited string satisfies the only stated tag requirement, which is
  search (FR-016).
- Normalizing on save keeps matching predictable and avoids near-duplicate tags.

**Alternatives considered**:

- *`Tag` and `DocumentTag` join tables*: Properly normalized and the right answer if tag browsing,
  renaming, or counting were required. None are in scope, and it would be the most complex part of
  the data model.

---

## R-010: Category representation

**Decision**: Store `Category` as a text column constrained to the six named values, with the allowed
set defined as constants in code and validated in `DocumentService`.

**Rationale**:

- The stakeholder document explicitly requires category to be stored as text rather than an integer
  enum, for readability when inspecting the database directly — a legitimate training concern.
- Text values keep the SQLite file self-describing; an integer enum requires the reader to know the
  ordinal mapping.
- Service-layer validation keeps the six-value constraint enforced in one place (FR-005).

**Alternatives considered**:

- *Integer enum, matching `UserRole` and `ProjectStatus`*: More consistent with existing entities and
  compact, but contradicts an explicit stakeholder constraint.
- *Lookup table with a foreign key*: Correct for a user-editable list; the list here is fixed.

---

## R-011: Identifier type

**Decision**: Use `int` identity keys for `Document`, `DocumentShare`, and `DocumentActivity`.

**Rationale**:

- Explicitly required by the stakeholder document for consistency with existing `UserId`,
  `ProjectId`, and `TaskId` keys.
- Matches every existing entity in [ApplicationDbContext.cs](../../ContosoDashboard/Data/ApplicationDbContext.cs).
- Sequential ids are enumerable, but FR-032 requires denial by authorization rather than by
  unguessable identifiers, and R-005's `NotFound`-on-denial removes the enumeration signal.

**Alternatives considered**:

- *GUID primary keys*: Non-enumerable, but inconsistent with every existing table and contrary to an
  explicit stakeholder constraint. Note the *stored filename* is still a GUID (R-001) — that is where
  unguessability actually matters.

---

## R-012: Deletion semantics

**Decision**: Delete permanently, after explicit confirmation: remove dependent share rows, delete
the database record, then delete the file from disk. Retain `DocumentActivity` rows after the
document is gone, storing the document title alongside the id so the audit trail stays readable.

**Rationale**:

- FR-022 and FR-023 require permanent removal with no recovery path; soft delete and trash are
  explicitly out of scope.
- Deleting the record before the file means a failed file delete leaves an unreferenced file rather
  than a broken listing — the safer failure direction, and detectable.
- Preserving activity rows keeps the audit trail intact (FR-039); an audit log that vanishes with its
  subject cannot answer compliance questions. Denormalizing the title keeps reports meaningful once
  the document row is gone.

**Alternatives considered**:

- *Cascade-delete activity records with the document*: Simpler schema, but destroys audit history and
  undermines FR-039 through FR-041.
- *Soft delete with an `IsDeleted` flag*: Recoverable and audit-friendly, but explicitly out of scope
  and would contradict "permanently removed".

---

## R-013: Notification integration

**Decision**: Extend the existing `NotificationType` enum with `DocumentShared` and
`DocumentAdded`, and create notifications through the existing `INotificationService`. Skip
notifying the acting user about their own action.

**Rationale**:

- Reuses the existing notification pipeline and UI rather than building a parallel one, satisfying
  FR-028, FR-030, and FR-031a with no new infrastructure.
- Adding enum members is additive and does not disturb existing seeded notification data.
- Self-notification is noise and would distort the notification count.

**Alternatives considered**:

- *A separate document notification table and feed*: Duplicates working functionality and splits the
  user's notification experience across two inboxes.

---

## Resolved Unknowns Summary

| ID | Question | Resolution |
|----|----------|------------|
| R-001 | Where and how is file content stored? | Local filesystem outside `wwwroot`, GUID filenames, relative paths |
| R-002 | How is malware handled offline? | `IFileScanner` seam with permissive local implementation, documented limitation |
| R-003 | How are type and size enforced? | Server-side whitelist + 25 MB cap on stream + signature check for PDF/JPEG/PNG |
| R-004 | How are orphaned records prevented? | Strict ordering with compensating file delete on insert failure |
| R-005 | How are downloads served? | `[Authorize]` controller endpoint, `NotFound` on denial |
| R-006 | How is Blazor upload handled safely? | Buffer to `MemoryStream`, rotate `@key`, release reference |
| R-007 | How is access decided? | Single `CanAccessAsync` rule; **add missing `Department` claim** |
| R-008 | How does search work? | Indexed LINQ contains-match with access filter applied in-query |
| R-009 | How are tags stored? | Normalized delimited string column |
| R-010 | How is category stored? | Text column, six allowed values validated in service |
| R-011 | What identifier type? | `int` identity keys, matching existing entities |
| R-012 | What does delete mean? | Permanent; activity records retained with denormalized title |
| R-013 | How are users notified? | Existing `INotificationService` with two new enum members |
| R-014 | How would real scanning work in production? | Azure Functions + Queue Storage, documented as future architecture; **not built** |

**No unresolved NEEDS CLARIFICATION items remain.**
