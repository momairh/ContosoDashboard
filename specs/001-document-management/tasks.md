# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-management/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts)

**Tests**: No automated test tasks are generated. The repository has no test project and the
constitution satisfies its verification gate through manual runtime validation. Validation tasks
reference scenarios in [quickstart.md](./quickstart.md).

**Scope**: All seven user stories. Phases 1-4 (T001-T045) are the **MVP** and were generated first;
their numbering is unchanged. Phases 5-10 (T046-T098) add User Stories 2-7, and Phase 11
(T099-T108) closes out the feature.

**Organization**: Tasks are grouped by phase. Setup, Foundational, and Polish phases carry no story
label; every user story phase does.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete work)
- **[Story]**: `[US1]` on user story tasks only
- Every task names an exact file path

## Path Conventions

Single ASP.NET Core project at `ContosoDashboard/`. Paths below are relative to the repository root.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Configuration and storage location that everything else depends on.

- [X] T001 Create the upload root directory `ContosoDashboard/AppData/uploads/` with a `.gitkeep` file so the empty folder is tracked
- [X] T002 Add `AppData/uploads/` to `.gitignore` (keeping `.gitkeep`) so uploaded content is never committed
- [X] T003 [P] Add a `DocumentStorage` configuration section to `ContosoDashboard/appsettings.json` with `RootPath` (`AppData/uploads`), `MaxFileSizeBytes` (26214400), and the allowed extension list per FR-002/FR-003
- [X] T004 [P] Create `ContosoDashboard/Models/DocumentStorageOptions.cs` as a strongly typed options class matching that section

**Checkpoint**: Configuration is bindable and the storage root exists.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities, schema, storage, and the scanner seam. Every user story depends on these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Domain types

- [X] T005 [P] Create `ContosoDashboard/Models/DocumentCategory.cs` holding the six allowed category values as string constants per research R-010
- [X] T006 [P] Create `ContosoDashboard/Models/DocumentActivityType.cs` enum with `Uploaded`, `Downloaded`, `Previewed`, `MetadataUpdated`, `ContentReplaced`, `Shared`, `ShareRevoked`, `Deleted` per data-model.md
- [X] T007 [P] Create `ContosoDashboard/Models/Document.cs` with all properties, `[MaxLength]` constraints, and navigation properties exactly as specified in data-model.md (note `FileType` is 255 and `FileSizeBytes` is `long`)
- [X] T008 [P] Create `ContosoDashboard/Models/DocumentShare.cs` with nullable `SharedWithUserId` and `SharedWithDepartment` per data-model.md
- [X] T009 [P] Create `ContosoDashboard/Models/DocumentActivity.cs` including the denormalized `DocumentTitle` column that keeps the audit trail readable after deletion (research R-012)

### Existing entity changes

- [X] T010 [P] Add a `virtual ICollection<Document> Documents` navigation collection to `ContosoDashboard/Models/User.cs`
- [X] T011 [P] Add a `virtual ICollection<Document> Documents` navigation collection to `ContosoDashboard/Models/Project.cs`
- [X] T012 [P] Add a `virtual ICollection<Document> Documents` navigation collection to the task entity in `ContosoDashboard/Models/`
- [X] T013 [P] Append `DocumentShared` and `DocumentAdded` members to the `NotificationType` enum in `ContosoDashboard/Models/Notification.cs`, preserving existing ordinal values so seeded data is unaffected

### Persistence

- [X] T014 Add `DbSet<Document>`, `DbSet<DocumentShare>`, and `DbSet<DocumentActivity>` to `ContosoDashboard/Data/ApplicationDbContext.cs`
- [X] T015 Configure relationships and delete behaviour in `OnModelCreating` in `ContosoDashboard/Data/ApplicationDbContext.cs`: restrict on user FKs, set-null on `ProjectId`/`TaskId`, cascade on `DocumentShare`, and **no enforced FK** from `DocumentActivity` to `Document` per data-model.md
- [X] T016 Configure the eleven indexes listed in data-model.md in `OnModelCreating` in `ContosoDashboard/Data/ApplicationDbContext.cs`, including the composite `(UploadedByUserId, UploadedAt)`

### Authentication gap

- [X] T017 Add a `Department` claim to the cookie identity in `ContosoDashboard/Pages/Login.cshtml.cs` (currently lines 59-62 emit only `NameIdentifier`, `Name`, `Email`, `Role`) — without this, department-scoped access silently denies everyone per research R-007

### File storage

- [X] T018 Create `ContosoDashboard/Services/IFileStorageService.cs` with `SaveFileAsync`, `GetFileAsync`, `DeleteFileAsync`, and `FileExistsAsync` per [contracts/file-storage-service.md](./contracts/file-storage-service.md)
- [X] T019 Implement `ContosoDashboard/Services/LocalFileStorageService.cs`: GUID stored filenames, `{userId}/{projectId|personal}/{guid}.{ext}` layout, relative paths returned, and a guard rejecting any resolved path that escapes the configured root

### Scanner seam

- [X] T020 [P] Create `ContosoDashboard/Services/IFileScanner.cs` and `FileScanResult` (`IsSafe`, `Reason`) per [contracts/file-scanner.md](./contracts/file-scanner.md)
- [X] T021 [P] Implement `ContosoDashboard/Services/PermissiveFileScanner.cs` approving all validated files and restoring stream position before returning

### Wiring

- [X] T022 Register `DocumentStorageOptions`, `IFileStorageService`, and `IFileScanner` as scoped services in `ContosoDashboard/Program.cs` (alongside the existing registrations at lines 41-45), then run the app once so `EnsureCreated()` builds the three new tables

**Checkpoint**: Schema exists, files can be written and read, and the scanner seam is in place. User story work can begin.

---

## Phase 3: User Story 1 - Upload and Find My Own Documents (Priority: P1) 🎯 MVP

**Goal**: An authenticated employee can upload a supported file with a title and category, then see it
in their own document list — and cannot see anyone else's.

**Independent Test**: Sign in, upload a supported file with a title and category, confirm it appears
in the list with correct title, category, size, and date. Sign in as a different user and confirm the
document is absent. Attempt an oversized file and an unsupported type and confirm both are rejected
with a clear reason.

### Service layer

- [X] T023 [P] [US1] Create `ContosoDashboard/Models/DocumentUploadRequest.cs` carrying title, description, category, tags, optional project and task ids, and the buffered content stream
- [X] T024 [P] [US1] Create `ContosoDashboard/Models/DocumentFilter.cs` with category, project, uploader, date range, and search term fields (only category and sort are consumed by US1)
- [X] T025 [US1] Create `ContosoDashboard/Services/IDocumentService.cs` declaring the US1 subset — `UploadDocumentAsync`, `GetDocumentsAsync`, `GetDocumentByIdAsync`, `CanAccessAsync` — with `requestingUserId` on every operation per [contracts/document-service.md](./contracts/document-service.md)
- [X] T026 [US1] Implement validation in `ContosoDashboard/Services/DocumentService.cs`: extension whitelist, size cap enforced **on the stream before writing**, and leading-byte signature check for PDF/JPEG/PNG per research R-003, returning a specific reason on rejection
- [X] T027 [US1] Implement `CanAccessAsync` in `ContosoDashboard/Services/DocumentService.cs` as the single authorization rule per research R-007 (uploader, Administrator, project member, Project Manager, Team Lead in department, share target)
- [X] T028 [US1] Implement `UploadDocumentAsync` in `ContosoDashboard/Services/DocumentService.cs` in the strict order validate → authorize target → scan → save file → insert record → write activity per research R-004, defaulting the title to the filename without extension when blank (FR-006)
- [X] T029 [US1] Add compensating cleanup to `UploadDocumentAsync` in `ContosoDashboard/Services/DocumentService.cs`: if the database insert fails, delete the just-written file before surfacing the error so no orphaned file or record can exist (FR-010)
- [X] T030 [US1] Implement `GetDocumentsAsync` in `ContosoDashboard/Services/DocumentService.cs` applying the access filter **inside the EF query** so inaccessible rows are never materialized (research R-008), sorted newest-first with category filtering
- [X] T031 [US1] Implement `GetDocumentByIdAsync` in `ContosoDashboard/Services/DocumentService.cs` returning `null` indistinguishably for both missing and inaccessible documents (research R-005)
- [X] T032 [US1] Register `IDocumentService` as a scoped service in `ContosoDashboard/Program.cs`

### Upload UI

- [X] T033 [US1] Create `ContosoDashboard/Shared/DocumentUploadDialog.razor` with `InputFile` restricted by `accept`, plus title, description, category dropdown, and tags inputs
- [X] T034 [US1] Implement the safe file-read pattern in `ContosoDashboard/Shared/DocumentUploadDialog.razor` per research R-006: capture `Name`/`Size`/`ContentType` into locals, copy `OpenReadStream(maxAllowedSize)` into a `MemoryStream`, reset position, release the `IBrowserFile` reference, then `StateHasChanged()`
- [X] T035 [US1] Add a rotating `@key` to the `InputFile` in `ContosoDashboard/Shared/DocumentUploadDialog.razor` that changes after each successful upload, preventing stale-reference and re-select failures
- [X] T036 [US1] Display validation and scan rejection reasons inline in `ContosoDashboard/Shared/DocumentUploadDialog.razor`, and surface a success message on completion (FR-002, FR-003, FR-009)

### List UI

- [X] T037 [US1] Create `ContosoDashboard/Pages/Documents.razor` at route `/documents` listing the signed-in user's accessible documents with title, category, file size, upload date, and project
- [X] T038 [US1] Add category filtering and upload-date sorting controls to `ContosoDashboard/Pages/Documents.razor`, bound to `DocumentFilter`
- [X] T039 [US1] Add an empty state to `ContosoDashboard/Pages/Documents.razor` shown when the user has no documents, prompting a first upload
- [X] T040 [US1] Embed `DocumentUploadDialog` in `ContosoDashboard/Pages/Documents.razor` and refresh the list on successful upload
- [X] T041 [US1] Add a Documents entry to `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: User Story 1 is fully functional and independently testable. Uploading and listing work; retrieving file content does not yet exist — that arrives with User Story 2.

---

## Phase 4: MVP Polish & Validation

- [X] T042 [P] Document the permissive scanner as a Known Limitation in `README.md`, stating plainly that no real malware detection occurs, and reference the future architecture in research R-014
- [X] T043 [P] Add a Documents section to `README.md` covering the upload location, supported types, and the 25 MB limit
- [X] T044 Run `dotnet build` from `ContosoDashboard/` and confirm zero errors and no new warnings beyond the 18 pre-existing ones
- [X] T045 Execute quickstart scenarios 1, 2, 3, and 12 from [quickstart.md](./quickstart.md) and confirm each passes — after scenarios 1 and 2 verify no record exists without a file and no file without a record. Scenario 5's list-visibility half applies here; its direct-URL 404 half is deferred to T060 once the download endpoint exists

---

## Phase 5: User Story 2 - Access and Manage Documents (Priority: P2)

**Goal**: Download a stored document unchanged, correct its metadata, replace its content with a new
version, and delete it permanently — with every one of those actions denied to users who lack access.

**Independent Test**: Upload a document, download it and confirm the bytes match the original, edit
its title and category and confirm the changes persist, replace the file and confirm downloads return
the new content, then delete it and confirm it is gone. As a different non-privileged user, confirm
download, edit, and delete are all refused.

### Download endpoint

- [ ] T046 [US2] Add `AddControllers()` and `MapControllers()` to `ContosoDashboard/Program.cs` — the application does not currently configure controller endpoint routing
- [ ] T047 [US2] Add `GetDocumentStreamAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, authorizing first, then incrementing `DownloadCount` and writing a `Downloaded` activity record (FR-041)
- [ ] T048 [US2] Create `ContosoDashboard/Controllers/DocumentsController.cs` with a class-level `[Authorize]` and a `GET /documents/download/{id}` action resolving the caller from the `NameIdentifier` claim per [contracts/documents-controller.md](./contracts/documents-controller.md)
- [ ] T049 [US2] Return **`NotFound`, never `Forbid`**, on denial in `ContosoDashboard/Controllers/DocumentsController.cs`, and stream content to the response rather than buffering it, using `Content-Disposition: attachment` with the original filename (FR-019, FR-032)
- [ ] T050 [US2] Handle the missing-file case in `ContosoDashboard/Controllers/DocumentsController.cs`: when a record exists but its file does not, log the condition and return `NotFound` rather than throwing (FR-024)

### Metadata editing and replacement

- [ ] T051 [P] [US2] Create `ContosoDashboard/Models/DocumentUpdateRequest.cs` carrying title, description, category, and tags
- [ ] T052 [US2] Add `UpdateMetadataAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, setting `LastModifiedAt` but deliberately **not** incrementing `Version`, and writing a `MetadataUpdated` activity (FR-020)
- [ ] T053 [US2] Add `ReplaceContentAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`: validate and scan the replacement, store it, increment `Version`, update size and type, write a `ContentReplaced` activity, and delete the previous file only after the record update succeeds (FR-021)

### Deletion

- [ ] T054 [US2] Add `DeleteDocumentAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`: restrict to uploader or Administrator, remove share rows, delete the record, then delete the file, **retaining** `DocumentActivity` rows so the audit trail survives (FR-022, FR-023, research R-012)

### Detail UI

- [ ] T055 [US2] Create `ContosoDashboard/Pages/DocumentDetails.razor` at route `/documents/{id:int}` showing full metadata, version, download count, and a download link to the controller endpoint
- [ ] T056 [US2] Add an inline metadata edit form to `ContosoDashboard/Pages/DocumentDetails.razor`, visible only to users permitted to edit
- [ ] T057 [US2] Add a file-replacement control to `ContosoDashboard/Pages/DocumentDetails.razor` reusing the `MemoryStream` buffering pattern from research R-006
- [ ] T058 [US2] Add a delete action with an explicit confirmation prompt to `ContosoDashboard/Pages/DocumentDetails.razor`, redirecting to the document list on success (FR-022)
- [ ] T059 [US2] Link each row in `ContosoDashboard/Pages/Documents.razor` to its detail page and add a direct download action

### Validation

- [ ] T060 [US2] Execute quickstart scenarios 4, 5, 7, 8, and 11 from [quickstart.md](./quickstart.md) — **scenario 5 must return 404 for both `/documents/download/{id}` and `/documents/preview/{id}`; a 403, an error page, or the file itself is a failure** (SC-009)

**Checkpoint**: Documents are fully usable end to end for their owner. The library is now retrievable, correctable, and prunable.

---

## Phase 6: User Story 3 - Project Documents and Team Access (Priority: P3)

**Goal**: A document associated with a project is visible and downloadable to every member of that
project, and to nobody else.

**Independent Test**: Upload a document against a project, confirm a member of that project sees and
downloads it from the project page, and confirm a non-member is denied both. Confirm a user cannot
associate a document with a project they do not belong to.

- [ ] T061 [US3] Add project membership and Project Manager checks to `CanAccessAsync` in `ContosoDashboard/Services/DocumentService.cs`, if not already covered by the T027 rule, and verify they resolve from the database rather than any request value
- [ ] T062 [US3] Enforce in `UploadDocumentAsync` in `ContosoDashboard/Services/DocumentService.cs` that a supplied `ProjectId` references a project the uploader belongs to, rejecting a forged id (FR-033)
- [ ] T063 [US3] Add `GetProjectDocumentsAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, verifying access to the parent project before returning anything (FR-013)
- [ ] T064 [US3] Add a project selector to `ContosoDashboard/Shared/DocumentUploadDialog.razor` listing only projects the signed-in user belongs to
- [ ] T065 [US3] Add a documents section to `ContosoDashboard/Pages/ProjectDetails.razor` listing that project's documents with download links
- [ ] T066 [US3] Permit a Project Manager to delete any document within their own project in `DeleteDocumentAsync` in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T067 [US3] Notify project members on upload to a project via the existing `INotificationService` using the `DocumentAdded` type, excluding the uploader (FR-028, research R-013)
- [ ] T068 [US3] Execute quickstart scenario 9 from [quickstart.md](./quickstart.md) and confirm a non-member receives 404

**Checkpoint**: Documents work as a team artifact, not just a personal one.

---

## Phase 7: User Story 4 - Search Across Accessible Documents (Priority: P4)

**Goal**: Find documents by a term appearing in title, description, tags, uploader name, or project
name — restricted to what the searcher may see.

**Independent Test**: Upload documents with distinct titles, descriptions, and tags. Search a term
unique to each field and confirm the right match. Confirm another user's document never appears in
results, and that a term matching nothing yields an empty-state message rather than an error.

- [ ] T069 [US4] Extend `GetDocumentsAsync` in `ContosoDashboard/Services/DocumentService.cs` with a case-insensitive contains match over title, description, tags, uploader display name, and project name, composed in LINQ so EF translates it to SQL (FR-016, research R-008)
- [ ] T070 [US4] Ensure the access filter is applied **within the same query** as the search predicate in `ContosoDashboard/Services/DocumentService.cs`, so inaccessible rows are excluded in the database and never materialized (FR-017)
- [ ] T071 [US4] Implement tag normalization on save in `ContosoDashboard/Services/DocumentService.cs` — trimmed, lowercased, de-duplicated, comma-delimited with no surrounding spaces (research R-009)
- [ ] T072 [US4] Add a search input to `ContosoDashboard/Pages/Documents.razor` bound to `DocumentFilter`, combinable with the existing category and project filters
- [ ] T073 [US4] Add uploader and date-range filters to `ContosoDashboard/Pages/Documents.razor` (FR-014, FR-015)
- [ ] T074 [US4] Add a distinct no-results message to `ContosoDashboard/Pages/Documents.razor`, separate from the never-uploaded empty state added in T039
- [ ] T075 [US4] Verify search and filter responses return within roughly two seconds against a few hundred documents, confirming the T016 indexes are being used (SC-004)

**Checkpoint**: The library is navigable at scale.

---

## Phase 8: User Story 5 - Share Documents with Specific People (Priority: P5)

**Goal**: An owner grants access to an individual or a whole department; recipients are notified, find
the document under "Shared with Me", and can download it. Revocation removes access immediately.

**Independent Test**: Share a document with one user and with a department. Confirm both are notified,
see it under "Shared with Me", and can download. Confirm a user in neither group is denied. Revoke the
individual share and confirm the direct download URL returns 404 again.

> **Depends on T017** (the `Department` claim added in Foundational). Without it, department sharing
> matches nothing and silently denies every recipient.

- [ ] T076 [US5] Add `ShareWithUserAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, requiring the sharer to already have access, rejecting self-shares, and remaining idempotent on repeat shares (FR-030, FR-035)
- [ ] T077 [US5] Add `ShareWithDepartmentAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, treating department as the unit of team sharing per the FR-031 clarification
- [ ] T078 [US5] Add `RevokeShareAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, restricted to the owner or an Administrator, writing a `ShareRevoked` activity (FR-036)
- [ ] T079 [US5] Add `GetSharesAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, visible to owner and Administrator only
- [ ] T080 [US5] Extend `CanAccessAsync` in `ContosoDashboard/Services/DocumentService.cs` to honour both individual and department share rows, reading the department from the claim added in T017 (FR-031a)
- [ ] T081 [US5] Raise `DocumentShared` notifications through the existing `INotificationService` on both share paths, skipping the acting user (FR-030, research R-013)
- [ ] T082 [US5] Create `ContosoDashboard/Shared/DocumentShareDialog.razor` offering a user picker and a department picker, and listing current shares with a revoke action
- [ ] T083 [US5] Embed the share dialog in `ContosoDashboard/Pages/DocumentDetails.razor`, visible only to users permitted to share
- [ ] T084 [US5] Add a "Shared with Me" view to `ContosoDashboard/Pages/Documents.razor` listing documents accessible solely through a share grant
- [ ] T085 [US5] Execute quickstart scenario 6 from [quickstart.md](./quickstart.md), confirming revocation restores the 404 and that department sharing actually grants access

**Checkpoint**: Controlled sharing replaces ad-hoc email distribution.

---

## Phase 9: User Story 6 - Documents in Context: Tasks and Dashboard (Priority: P6)

**Goal**: Attach documents to tasks and surface recent documents on the dashboard home page.

**Independent Test**: Attach a document to a task and confirm it is listed there and inherits the
task's project. Upload several documents and confirm the dashboard shows the five most recent and an
accurate count.

- [ ] T086 [US6] Add `GetTaskDocumentsAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, verifying access to the parent task first
- [ ] T087 [US6] Set `ProjectId` from the task's own project when a document is attached to a task in `ContosoDashboard/Services/DocumentService.cs`, so task attachments are automatically project-associated
- [ ] T088 [US6] Add a task selector to `ContosoDashboard/Shared/DocumentUploadDialog.razor`, scoped to tasks within the chosen project
- [ ] T089 [US6] Add a documents section to the task detail page in `ContosoDashboard/Pages/` supporting both attaching an existing document and uploading a new one
- [ ] T090 [P] [US6] Add recent-documents and document-count methods to `ContosoDashboard/Services/DashboardService.cs`, both scoped to what the signed-in user may access
- [ ] T091 [US6] Add a five-most-recent documents widget and a document count card to `ContosoDashboard/Pages/Index.razor` (FR-026, FR-027)

**Checkpoint**: Documents are discoverable from where users already work.

---

## Phase 10: User Story 7 - Administrative Oversight (Priority: P7)

**Goal**: An administrator can review who did what to which document and when, and see summary
reporting. Non-administrators cannot.

**Independent Test**: Perform uploads, downloads, deletions, and shares as several users. As an
administrator, confirm every action is recorded with actor, document, and timestamp, and that summary
figures reflect them. As a non-administrator, confirm the activity views are refused.

- [ ] T092 [US7] Add `GetActivityAsync` to `ContosoDashboard/Services/IDocumentService.cs` and implement it in `ContosoDashboard/Services/DocumentService.cs`, returning newest-first activity for users who can access the document (FR-040)
- [ ] T093 [US7] Audit every state-changing method in `ContosoDashboard/Services/DocumentService.cs` and confirm each writes **exactly one** activity record, and that failed operations write none (FR-039)
- [ ] T094 [US7] Add an activity timeline to `ContosoDashboard/Pages/DocumentDetails.razor` showing each action with its acting user and timestamp
- [ ] T095 [US7] Create `ContosoDashboard/Pages/DocumentActivityReport.razor` at an administrator-only route, guarded server-side rather than by hiding navigation (FR-038)
- [ ] T096 [US7] Add summary reporting to `ContosoDashboard/Pages/DocumentActivityReport.razor` covering most-uploaded file types, most active uploaders, and access patterns (FR-041)
- [ ] T097 [US7] Verify that activity records for a deleted document remain queryable and still display their denormalized `DocumentTitle`, per quickstart scenario 10 (research R-012)
- [ ] T098 [US7] Confirm a non-administrator receives a denial — not a hidden link — when navigating directly to the activity report route

**Checkpoint**: The feature can answer audit and compliance questions.

---

## Phase 11: Feature Polish & Cross-Cutting

- [ ] T099 [US2] Add a `GET /documents/preview/{id}` action to `ContosoDashboard/Controllers/DocumentsController.cs` serving `Content-Disposition: inline` for `application/pdf`, `image/jpeg`, and `image/png` **only**, forcing download for every other type so the browser never renders untrusted content inline (FR-018)
- [ ] T100 [US2] Add an inline preview pane to `ContosoDashboard/Pages/DocumentDetails.razor` for previewable types, writing a `Previewed` activity record
- [ ] T101 [P] Add file-type icons and human-readable size formatting to `ContosoDashboard/Pages/Documents.razor` (FR-025)
- [ ] T102 [P] Add upload progress feedback to `ContosoDashboard/Shared/DocumentUploadDialog.razor` for large files (FR-037)
- [ ] T103 Verify every `IDocumentService` method takes and enforces `requestingUserId`, and that no Razor page or controller performs its own authorization decision (Constitution IV, FR-032)
- [ ] T104 Verify no `DbContext` query for documents bypasses `CanAccessAsync` or its equivalent in-query filter
- [ ] T105 [P] Update `README.md` with the complete feature description, sharing model, and the department-as-team clarification
- [ ] T106 Run `dotnet build` from `ContosoDashboard/` and confirm zero errors and no new warnings beyond the 18 pre-existing ones
- [ ] T107 Execute the full quickstart suite, scenarios 1-12, from [quickstart.md](./quickstart.md)
- [ ] T108 Confirm the completion checklist at the end of [quickstart.md](./quickstart.md) passes in full, including the no-orphan check across both stores

---

## Dependencies

**Phase order**: Setup (T001-T004) → Foundational (T005-T022) → US1 (T023-T041) → MVP Polish
(T042-T045) → US2 (T046-T060) → US3 (T061-T068) → US4 (T069-T075) → US5 (T076-T085) → US6
(T086-T091) → US7 (T092-T098) → Feature Polish (T099-T108).

Phases 1-4 are the MVP. Phases 5-10 follow spec priority order, but only US2 is a hard prerequisite
for the rest.

**Within Foundational**:

- T007-T009 depend on T005-T006 (entities reference the category constants and activity enum)
- T014 depends on T007-T009 (DbSets need the entities)
- T015-T016 depend on T014
- T019 depends on T018 and T004
- T021 depends on T020
- T022 depends on T015-T016, T019, T021

**Within US1**:

- T025 depends on T023-T024
- T026-T031 depend on T025, and each modifies the same `DocumentService.cs`, so they are **sequential**
- T028 depends on T026-T027 (validation and authorization must exist before the pipeline uses them)
- T029 depends on T028
- T032 depends on T025
- T033-T036 depend on T032, and all edit the same component, so they are **sequential**
- T037-T040 depend on T032; T040 additionally depends on T036
- T041 is independent of the rest of US1

**Critical path**: T005 → T007 → T014 → T015 → T022 → T025 → T026 → T027 → T028 → T029 → T037 → T045

**Cross-story dependencies**:

- **US3, US4, US5, US6, US7 all depend on US2** — specifically on the download endpoint (T046-T049).
  A document that cannot be retrieved makes project visibility, search results, and sharing hollow.
- **US5 depends on T017** (Department claim). This is the one dependency that fails *silently*: with
  no claim, department sharing matches nothing and denies every recipient without erroring.
- **US7 depends on activity records written by every earlier story.** T093 audits that work rather
  than creating it, so US7 is only meaningful once US2-US5 have run.
- **US4 (search) is otherwise independent** of US3, US5, and US6 — it extends `GetDocumentsAsync`
  from US1 and can be built as soon as US2 lands.
- **US6 depends on US3** for the project-association behaviour that T087 reuses.

**Inter-story file contention**: `DocumentService.cs` and `Documents.razor` are edited by nearly every
phase. Stories overlapping in time will conflict in those two files regardless of their logical
independence.

**Blocking risk**: T017 (Department claim) is not exercised by US1's acceptance scenarios, but every
later story's sharing depends on it. It is placed in Foundational deliberately so the gap is closed
before it can cause a silent authorization failure.

---

## Parallel Execution Examples

**Setup**: T003 and T004 together.

**Foundational, first wave** — nine independent files:

```
T005, T006, T007, T008, T009, T010, T011, T012, T013
```

**Foundational, second wave** — after T014-T016 land:

```
T018 → T019   (storage)
T020 → T021   (scanner)
T017          (login claim, independent of both)
```

**US1, first wave**: T023 and T024 together.

**US1, UI split** — after T032, two developers can work in parallel:

```
Developer A: T033 → T034 → T035 → T036   (upload dialog)
Developer B: T037 → T038 → T039, T041    (list page)
Converge at T040.
```

**Polish**: T042 and T043 together.

**US2**: T051 is independent; the controller chain T046 → T048 → T049 → T050 is strictly sequential
(same file), as is the detail-page chain T055 → T056 → T057 → T058.

**After US2 lands**, three streams can run concurrently if the file contention above is managed:

```
Stream A: US3 (T061-T068)   project visibility
Stream B: US4 (T069-T075)   search
Stream C: US5 (T076-T085)   sharing
```

**Feature Polish**: T101, T102, and T105 are independent of each other and of the verification tasks.

---

## Implementation Strategy

**MVP scope**: Phases 1-3 deliver User Story 1 — upload and view your own documents. This is a
complete, demonstrable increment: a personal document store replacing local drives and email.

**Deferred by design**: Inline preview (T099-T100) is separated from download and held to Phase 11.
Download is required by five other stories; preview is required by none, and it carries the
content-type risk that makes it worth handling deliberately rather than alongside the endpoint it
shares a file with.

**Incremental delivery**: Each phase from 5 onward is independently demonstrable. Stopping after any
of them leaves a coherent product — after US2 a personal document manager, after US3 a team library,
after US5 a controlled sharing system.

**Sequencing note**: `DocumentService.cs` is touched by six consecutive US1 tasks and by nearly every
later phase. Treat T026-T031 as one focused sitting rather than six handoffs — splitting them across
people will cause conflicts. The same applies to the controller chain T046-T050.

**Estimated effort**:

| Scope | Tasks | Estimate |
|---|---|---|
| MVP (Phases 1-4) | T001-T045 | 6-8 hours |
| US2 (Phase 5) | T046-T060 | 4-5 hours |
| US3, US4 (Phases 6-7) | T061-T075 | 4-5 hours |
| US5 (Phase 8) | T076-T085 | 3-4 hours |
| US6, US7 (Phases 9-10) | T086-T098 | 4-5 hours |
| Feature Polish (Phase 11) | T099-T108 | 2-3 hours |
| **Total** | **T001-T108** | **23-30 hours** |

For a developer familiar with ASP.NET Core and Blazor Server.
