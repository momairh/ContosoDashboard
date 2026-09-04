# Contract: DocumentsController

**Feature**: `001-document-management` | HTTP endpoints for serving file content.

A controller is required because Blazor Server components render markup over a SignalR circuit and
cannot return an HTTP file response. Since uploads live outside `wwwroot`, this controller is the
**only** route to file content, which makes the authorization check unavoidable (R-005, FR-032).

The controller carries `[Authorize]` at class level. It contains no business logic: it resolves the
caller, delegates to `IDocumentService`, and translates the result into an HTTP response.

## Endpoints

### `GET /documents/download/{id}`

Returns the file as an attachment.

- Resolves the caller's id from the `NameIdentifier` claim.
- Delegates to `GetDocumentStreamAsync(id, userId)`.
- `200` with `Content-Disposition: attachment` using the **original** filename, so the user gets a
  recognizable name even though storage uses a GUID (FR-019).
- `404` when the document is missing **or** inaccessible.
- `404` when the record exists but its file has disappeared, with the condition logged (FR-024).

### `GET /documents/preview/{id}`

Returns the file for inline display.

- Identical authorization path.
- `200` with `Content-Disposition: inline` for `application/pdf`, `image/jpeg`, and `image/png` only.
- Any other type is served as an attachment instead, so the browser never attempts to render
  untrusted content inline (FR-018).
- `404` under the same conditions as download.

## Guarantees

- **`404`, never `403`, on denial.** A `403` would confirm the id exists and turn sequential integer
  keys into an enumeration oracle (R-005, R-011).
- The controller never queries `DbContext` directly and never opens a file itself.
- Path values are never accepted from the request — only a document id. The stored path is resolved
  from the database.
- Content is streamed to the response rather than buffered, so a 25 MB download does not load
  entirely into memory.

## Registration

Requires `AddControllers()` and `MapControllers()` in `Program.cs`, which the application does not
currently configure.
