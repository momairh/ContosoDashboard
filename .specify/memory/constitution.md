<!--
Sync Impact Report
- Version change: none → 1.0.0 (initial ratification)
- Modified principles: none (initial adoption; all five principles newly defined)
  - Added: I. Training-First Purpose
  - Added: II. Offline-First, Zero External Dependencies
  - Added: III. Security by Demonstration
  - Added: IV. Layered Separation of Concerns
  - Added: V. Spec-Driven Development Workflow
- Added sections:
  - Core Principles (I–V)
  - Technology and Architecture Constraints
  - Development Workflow and Quality Gates
  - Governance
- Removed sections: none
- Templates requiring updates:
  - .specify/templates/plan-template.md ✅ no change required (reads constitution at runtime)
  - .specify/templates/spec-template.md ✅ no change required
  - .specify/templates/tasks-template.md ✅ no change required
- Follow-up TODOs: none
-->

# ContosoDashboard Constitution

## Core Principles

### I. Training-First Purpose

ContosoDashboard exists to teach Spec-Driven Development and secure application patterns. Every
change MUST be evaluated on whether it makes the codebase easier to learn from, not on whether it
would be appropriate for production.

- Code MUST remain readable and traceable over being clever, compact, or maximally optimized.
- Features MUST be small enough that a learner can follow the full path from page to service to
  database in a single sitting.
- Simplifications that diverge from production practice MUST be documented as known limitations in
  [README.md](../../README.md) rather than silently accepted.
- This repository MUST NOT be presented or deployed as production software.

**Rationale**: The project's stated purpose is education. An undocumented shortcut teaches the wrong
lesson; a documented one teaches two.

### II. Offline-First, Zero External Dependencies

The application MUST run to a fully working, seeded state on a fresh machine with no cloud account,
no network access, and no separate database server.

- Persistence MUST use the local SQLite database file created automatically on first run.
- Authentication MUST use the local mock cookie scheme; no external identity provider may be
  required to run the app.
- New features MUST NOT introduce a mandatory dependency on a paid, hosted, or network-bound
  service.
- Cloud-capable functionality MUST be introduced behind an interface abstraction (for example
  `IFileStorageService`) with a local implementation registered by default, so a cloud
  implementation can be swapped in via dependency injection without changing business logic.

**Rationale**: Training must never be blocked by a subscription, a VPN, or a failed provisioning
step. Abstraction preserves the cloud-migration teaching point without sacrificing offline runs.

### III. Security by Demonstration

Authorization is a teaching subject in this codebase, so it MUST be enforced visibly and
consistently rather than assumed.

- Every protected page MUST carry an `[Authorize]` attribute; UI-only hiding of navigation is NOT
  sufficient.
- Every service method that reads or mutates user-scoped data MUST verify that the requesting user
  is authorized for that specific record, defending against IDOR. Route, form, and query parameters
  MUST be treated as untrusted.
- Defense in depth MUST be preserved across all three layers: middleware, page attributes, and
  service checks. Removing a layer because another one already covers the case is prohibited.
- Role checks MUST respect the established hierarchy (Employee → TeamLead → ProjectManager →
  Administrator).
- Security shortcuts taken for training MUST be listed under Known Limitations in
  [README.md](../../README.md).

**Rationale**: Learners copy what they see. A single unguarded service method silently teaches an
exploitable pattern.

### IV. Layered Separation of Concerns

The Models / Data / Services / Pages layering MUST be maintained so that responsibilities stay
discoverable.

- Razor pages and components MUST NOT query `ApplicationDbContext` directly; data access belongs in
  the service layer.
- Each service MUST be defined by an interface and registered through dependency injection.
- Entity Framework Core access MUST use async methods and MUST use eager loading (`.Include()`)
  where related data is rendered, to avoid N+1 queries.
- Frequently filtered or joined columns MUST be indexed in
  [ApplicationDbContext.cs](../../ContosoDashboard/Data/ApplicationDbContext.cs).
- Schema changes MUST keep the seeded sample data valid so a fresh run still produces a populated,
  demonstrable dashboard.

**Rationale**: The layering is itself part of the curriculum; violating it erases the lesson and
makes authorization checks easy to bypass.

### V. Spec-Driven Development Workflow

Non-trivial work MUST flow through the Spec Kit lifecycle rather than starting at the code.

- Feature work MUST begin with a specification, followed by a plan, then generated tasks, then
  implementation.
- Specifications MUST describe user-visible behavior and acceptance criteria before implementation
  details are fixed.
- Ambiguities MUST be resolved through clarification and recorded in the spec instead of being
  settled by an unrecorded assumption in code.
- Spec Kit assets under [.github/](../../.github) and [.specify/](../../.specify) are workflow
  infrastructure and MUST NOT be edited as part of ordinary feature implementation.

**Rationale**: The repository is the reference example for Spec-Driven Development. Bypassing the
workflow to hand-write a feature contradicts the material being taught.

## Technology and Architecture Constraints

- **Runtime**: .NET with ASP.NET Core; the project targets `net10.0` and MUST build with the
  installed SDK using `dotnet build`.
- **UI**: Blazor Server for interactive pages; Razor Pages for flows requiring a full HTTP request
  such as login and logout.
- **Data**: Entity Framework Core over SQLite, using `Data Source=ContosoDashboard.db`. Provider-
  specific SQL, SQL Server-only column types, and raw vendor SQL MUST be avoided so the project
  stays portable across developer machines and CPU architectures, including ARM64.
- **Schema management**: The app uses `EnsureCreated()` with seed data for development. Deleting the
  local database file MUST remain a sufficient reset procedure.
- **Styling**: Bootstrap 5.3 with Bootstrap Icons; no additional CSS or JavaScript framework may be
  introduced.
- **Secrets**: No real credentials, tokens, connection secrets, or personal data may be committed.
  Sample users MUST remain obviously fictional.
- **Generated artifacts**: The SQLite database file and its `-shm` / `-wal` companions MUST remain
  git-ignored.

## Development Workflow and Quality Gates

- **Build gate**: `dotnet build` MUST succeed with zero errors before any change is considered
  complete.
- **No new warnings**: A change MUST NOT increase the compiler warning count. Pre-existing warnings
  may be left in place, but newly introduced ones MUST be fixed.
- **Runtime verification**: Changes affecting data access, authentication, or navigation MUST be
  verified by running the application and confirming that the database is created and seeded and
  that affected pages return successfully.
- **Scope discipline**: Changes MUST stay within the requested scope. Unrelated pre-existing issues
  MUST NOT be opportunistically refactored; bugs directly caused by the change MUST be fixed.
- **Documentation sync**: Changes to the stack, configuration, prerequisites, setup steps, or known
  limitations MUST be reflected in [README.md](../../README.md) in the same change.
- **Authorization review**: Any new or modified page, route, or service method MUST be reviewed
  explicitly against Principle III before completion.

## Governance

This constitution supersedes other conventions and ad hoc practice in this repository. Where
guidance conflicts, the constitution governs.

- **Authority**: All contributions, including AI-assisted and Spec Kit-generated work, MUST comply.
  Plans and implementations that violate a principle MUST be revised or MUST record an explicit,
  justified exception in the relevant plan document.
- **Amendment procedure**: Amendments MUST be made by updating this file, MUST include a Sync Impact
  Report at the top describing the change, and MUST state the rationale for the affected principle.
- **Versioning policy**: This document follows semantic versioning.
  - MAJOR: a principle is removed or redefined in a backward-incompatible way.
  - MINOR: a principle or section is added, or guidance is materially expanded.
  - PATCH: clarifications, wording, and non-semantic refinements.
- **Compliance review**: Constitutional compliance MUST be confirmed at the plan stage and again
  before a feature is considered complete. Complexity that appears to violate Principle I or IV MUST
  be justified in writing or removed.
- **Runtime guidance**: Day-to-day operational detail lives in [README.md](../../README.md); it
  elaborates on this constitution but MUST NOT contradict it.

**Version**: 1.0.0 | **Ratified**: 2025-11-26 | **Last Amended**: 2026-09-04
