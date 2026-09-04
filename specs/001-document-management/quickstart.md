# Quickstart: Document Upload and Management

**Feature**: `001-document-management` | Manual validation scenarios.

The repository has no automated test project, so the constitution's runtime-verification gate is
satisfied by executing these scenarios by hand. Each maps to acceptance criteria in
[spec.md](./spec.md). A scenario is not passed until its stated outcome is observed in a running app.

## Setup

```powershell
cd C:\Project\AIEnableEng\ContosoDashboard\ContosoDashboard
dotnet run
```

To reset to a clean state, stop the app, delete `ContosoDashboard.db`, delete `AppData/uploads/`,
and run again — `EnsureCreated()` rebuilds the schema and reseeds users.

Seeded accounts (any password is accepted by the mock login):

| User | Role | Purpose in these scenarios |
|------|------|----------------------------|
| Admin | Administrator | Cross-cutting access checks |
| Project Manager | ProjectManager | Project-scoped access |
| Team Lead | TeamLead | Department-scoped access |
| Team Member | TeamMember | Baseline, unprivileged user |

Prepare a few local files: a small PDF, a PNG, a file over 25 MB, and a `.exe`.

---

## Scenario 1 — Upload (US1)

1. Sign in as Team Member, open the documents page, choose the PDF.
2. Set a title, description, category, and two tags. Submit.

**Expect**: success message; document appears at the top of the list with correct name, size, type,
category, tags, uploader, and timestamp. A new file exists under `AppData/uploads/`, named as a GUID
— **not** the original filename.

## Scenario 2 — Upload rejection (US1)

1. Attempt the `.exe`. **Expect**: rejected with a message naming the unsupported type; no file
   appears on disk and no row is added.
2. Attempt the oversized file. **Expect**: rejected with a message stating the 25 MB limit.
3. Rename the `.exe` to `.pdf` and retry. **Expect**: rejected — the content signature does not match.

**Then confirm** the document list still matches the files on disk exactly. Neither rejection may
leave a record without a file or a file without a record.

## Scenario 3 — Browse, search, filter (US2)

Upload several documents across different categories and projects, then:

1. Confirm newest-first ordering by default.
2. Search a word appearing only in one document's description. **Expect**: only that document.
3. Search by uploader name and by project name. **Expect**: matching results for both.
4. Filter by category, then by project. **Expect**: correctly narrowed results.
5. Combine a filter with a search term. **Expect**: both applied.

**Expect** every response within roughly two seconds (SC-002, SC-004).

## Scenario 4 — Download and preview (US3)

1. Download the PDF. **Expect**: it saves under its **original** filename and opens correctly.
2. Preview the PDF, then the PNG. **Expect**: both render in the browser without downloading.
3. Preview a Word document. **Expect**: it downloads instead of rendering.
4. Re-open the document detail. **Expect**: the download count increased and the activity log shows
   the download and preview entries.

## Scenario 5 — Access control (US3, US6) — the critical scenario

1. As Team Member, upload a personal document. Note its id from the URL.
2. Sign out, sign in as a **different** Team Member.
3. Confirm the document does **not** appear in any list or search result.
4. Navigate directly to `/documents/download/{id}` with that id.

**Expect**: `404`. **A `403`, an error page, or the file itself is a failure** — a `403` confirms the
id exists (SC-009, FR-032).

5. Repeat for `/documents/preview/{id}`. **Expect**: `404`.
6. Sign in as Admin. **Expect**: the document is visible and downloadable.

## Scenario 6 — Sharing (US6)

1. As the owner, share the document with a specific user. **Expect**: a confirmation, and a
   notification for the recipient.
2. Sign in as the recipient. **Expect**: the document is now visible and downloadable.
3. Sign back in as the owner and revoke the share.
4. As the former recipient, retry the direct download URL. **Expect**: `404` again.
5. Share with a department. **Expect**: every user in that department gains access and is notified;
   users outside it still receive `404`.

**Note**: if department sharing grants nothing, verify the `Department` claim was added to
[Login.cshtml.cs](../../ContosoDashboard/Pages/Login.cshtml.cs) — its absence is the expected cause.

## Scenario 7 — Edit and replace (US4)

1. Change title, description, category, and tags. **Expect**: changes persist; the modified timestamp
   updates; the version number is **unchanged**.
2. Replace the file with a different one. **Expect**: version increments; size and type update;
   downloading now returns the **new** content.
3. Check the activity log. **Expect**: separate entries for the metadata update and the replacement.

## Scenario 8 — Delete (US4)

1. Delete a document. **Expect**: a confirmation prompt first.
2. Confirm. **Expect**: it disappears from all lists, and its file is gone from `AppData/uploads/`.
3. Attempt its former download URL. **Expect**: `404`.
4. As a non-owner, non-admin, attempt to delete another user's document. **Expect**: refused.

## Scenario 9 — Project and task association (US5)

1. Upload a document associated with a project. **Expect**: it appears on that project's documents
   view and project members are notified.
2. Upload another associated with a task. **Expect**: it appears on that task.
3. As a user who is not a member of that project, attempt access. **Expect**: `404`.

## Scenario 10 — Activity audit (US7)

Open a document that has been uploaded, downloaded, edited, shared, and had its share revoked.

**Expect**: an entry for each action, newest first, each naming the acting user and time. Then delete
the document and confirm the activity records still exist in the database (FR-039, R-012).

## Scenario 11 — Missing file recovery

1. Stop the app, delete one file from `AppData/uploads/` while leaving its record, restart.
2. Open the document list. **Expect**: it still loads; the document is still listed.
3. Attempt to download it. **Expect**: a clear error rather than an unhandled exception (FR-024).

## Scenario 12 — Concurrent upload isolation

Upload the **same file** as two different users.

**Expect**: two independent records with different stored GUID filenames under different user
directories, each accessible only to its own uploader.

---

## Completion Checklist

- [ ] Scenarios 1–12 all pass
- [ ] Scenario 5 verified with an explicit `404` for both endpoints
- [ ] No record without a file, and no file without a record, after all scenarios
- [ ] `dotnet build` reports zero errors
- [ ] Scanner limitation documented in the README
