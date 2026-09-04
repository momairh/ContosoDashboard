# Implementation Plan: Document Upload and Management

**Branch**: `001-document-management` | **Date**: 2026-09-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-document-management/spec.md`

## Summary

Add document upload, organization, retrieval, sharing, and audit capabilities to the existing
ContosoDashboard Blazor Server application. Users upload files (PDF, Office, text, JPEG, PNG, max
25 MB) with metadata, browse and search their documents, associate documents with projects, share
them with individuals or departments, attach them to tasks, and see recent documents on the
dashboard. Administrators review document activity.

The technical approach keeps every new dependency local. File content is stored on the local
filesystem outside `wwwroot`, behind an `IFileStorageService` abstraction that a future
`AzureBlobStorageService` can replace without touching business logic. Metadata lives in the existing
SQLite database via new EF Core entities. All authorization is enforced in a new `DocumentService`,
consistent with the existing service-layer IDOR protection pattern. Downloads are served by an
authenticated controller endpoint, because files stored outside `wwwroot` are not statically
reachable — which is the point.

## Technical Context

**Language/Version**: C# 12 on .NET 10 (`net10.0`)  
**Primary Dependencies**: ASP.NET Core (Blazor Server + Razor Pages), Entity Framework Core 8,
Bootstrap 5.3. No new NuGet packages required.  
**Storage**: SQLite (`Data Source=ContosoDashboard.db`) for metadata; local filesystem under
`AppData/uploads/` for file content.  
**Testing**: No automated test project exists in this repository. Validation is manual and scripted
via [quickstart.md](./quickstart.md), consistent with the constitution's runtime verification gate.  
**Target Platform**: Local developer machine (Windows, including ARM64), self-hosted Kestrel, fully
offline.  
**Project Type**: Single ASP.NET Core web application (existing structure extended in place).  
**Performance Goals**: Document list ≤ 2 s for 500 documents; search ≤ 2 s; preview ≤ 3 s; 25 MB
upload ≤ 30 s (SC-002 through SC-005).  
**Constraints**: Must run offline with no cloud account, no network, and no separate database
server. No external malware scanning service. Uploads capped at 25 MB. Files must not be served
directly from `wwwroot`.  
**Scale/Scope**: Training scale — a handful of seeded users, hundreds of documents. 3 new entities,
1 new service plus 2 new abstractions, 1 controller, 3 new pages, and edits to 4 existing pages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Initial | Post-Design |
|-----------|------|---------|-------------|
| I. Training-First Purpose | Feature stays readable and traceable page → service → storage → database; simplifications documented as Known Limitations in README | PASS | PASS |
| II. Offline-First, Zero External Dependencies | No mandatory network, cloud, or paid service; cloud-capable behavior sits behind an interface with a local default implementation | PASS | PASS |
| III. Security by Demonstration | `[Authorize]` on every new page and the download endpoint; per-document authorization in the service layer; all three defense layers retained | PASS | PASS |
| IV. Layered Separation of Concerns | No `DbContext` access from pages; interface-backed service registered via DI; async EF with `.Include()`; indexes on filtered columns; seed data remains valid | PASS | PASS |
| V. Spec-Driven Development Workflow | Spec completed and clarified before planning; Spec Kit assets untouched by this feature | PASS | PASS |

**Initial evaluation**: PASS — no violations, no entries required in Complexity Tracking.

**Notes on gate reasoning:**

- Principle II is the binding constraint on FR-009. Real malware scanning would require an external
  engine or service, so the resolved design uses a replaceable `IFileScanner` with a permissive local
  implementation and a documented Known Limitation. This preserves offline operation while leaving a
  real seam. See [research.md](./research.md) R-002.
- Principle III drives the download endpoint design: because files live outside `wwwroot`, every
  retrieval necessarily passes through an authorized code path. Static file serving of uploads is
  therefore prohibited, not merely discouraged.
- Principle IV is why document authorization lives in `DocumentService` and never in a page. Pages
  call the service; the service decides.

**Post-design re-evaluation**: PASS. The Phase 1 design introduces three entities, one service, two
small abstractions (`IFileStorageService`, `IFileScanner`), and one controller. No layer is bypassed
and no external dependency is added. The two abstractions are each justified by a constitutional
requirement rather than by speculative generality, so Principle I's simplicity expectation holds.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── file-storage-service.md
│   ├── file-scanner.md
│   ├── document-service.md
│   └── documents-controller.md
├── checklists/
│   └── requirements.md  # Created by /speckit.specify
└── tasks.md             # Created later by /speckit.tasks
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs                  # NEW - document metadata entity
│   ├── DocumentShare.cs             # NEW - share grant (user or department)
│   ├── DocumentActivity.cs          # NEW - audit record
│   ├── User.cs                      # EDIT - add Documents navigation collection
│   ├── Project.cs                   # EDIT - add Documents navigation collection
│   ├── TaskItem.cs                  # EDIT - add Documents navigation collection
│   └── Notification.cs              # EDIT - add DocumentShared/DocumentAdded types
├── Data/
│   └── ApplicationDbContext.cs      # EDIT - DbSets, relationships, indexes, seed
├── Services/
│   ├── IFileStorageService.cs       # NEW - storage abstraction
│   ├── LocalFileStorageService.cs   # NEW - filesystem implementation
│   ├── IFileScanner.cs              # NEW - content safety abstraction
│   ├── PermissiveFileScanner.cs     # NEW - offline implementation
│   ├── IDocumentService.cs          # NEW - business logic + authorization
│   ├── DocumentService.cs           # NEW
│   └── DashboardService.cs          # EDIT - recent documents + count
├── Controllers/
│   └── DocumentsController.cs       # NEW - authorized download/preview endpoint
├── Pages/
│   ├── Documents.razor              # NEW - my documents, upload, search
│   ├── DocumentDetails.razor        # NEW - metadata edit, replace, share, delete
│   ├── DocumentActivityReport.razor # NEW - administrator audit view
│   ├── Index.razor                  # EDIT - recent documents widget, count card
│   ├── ProjectDetails.razor         # EDIT - project documents section
│   ├── Login.cshtml.cs              # EDIT - add Department claim
│   └── _Host.cshtml                 # unchanged
├── Shared/
│   ├── DocumentUploadDialog.razor   # NEW - reusable upload component
│   └── NavMenu.razor                # EDIT - Documents nav entry
├── AppData/uploads/                 # NEW - file content root (git-ignored)
└── Program.cs                       # EDIT - register services, map controllers
```

**Structure Decision**: The existing single-project layout is extended in place. ContosoDashboard is
one ASP.NET Core web application with `Models/`, `Data/`, `Services/`, `Pages/`, and `Shared/`
folders; this feature adds files to those same folders rather than introducing a new project or a
separate API tier. A `Controllers/` folder is new to the repository and is required because Blazor
Server components cannot return a file stream as an HTTP response — a controller endpoint is the
supported way to serve authorized downloads. `AppData/uploads/` is deliberately outside `wwwroot` so
that no upload is reachable without passing an authorization check.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

## Out of Scope: Asynchronous Cloud Scanning

The intended production scanning architecture — an Azure Function triggered by a Queue Storage
message, scanning uploaded content out of band and writing the verdict back — is recorded in
[research.md](./research.md) as **R-014**. It is **explicitly not implemented by this plan** and
generates no tasks.

It sits behind the `IFileScanner` seam, so adopting it later is a matter of swapping the registered
implementation *plus* the schema and specification changes noted below. Deferring it here is what
keeps this feature compliant with Constitution Principle II (offline operation); building it would
introduce Azure Storage, an Azure Functions host, and a scanning engine as hard runtime dependencies.

Adopting it in future would require, at minimum:

- Amending Principle II, or scoping the cloud dependency to a non-default configuration.
- Rewording **FR-009**, since scanning becomes detective rather than preventive — a file is stored and
  visible before its verdict returns.
- Adding a `ScanStatus` field to `Document` and a pending state to the UI, with a decision on whether
  pending documents may be downloaded.
- Replacing `LocalFileStorageService` with blob storage so the Function can reach the content.

These are specification-level changes, not implementation details, and belong to a future feature.
