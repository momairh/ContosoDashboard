# Contract: IFileScanner

**Feature**: `001-document-management` | Content safety seam (R-002, FR-009, FR-009a).

Exists to give the upload pipeline a real place where scanning happens, so a genuine engine can be
introduced later by changing one DI registration. The shipped implementation is permissive because
the application must run fully offline (Constitution II).

## Operations

### `ScanAsync(stream, fileName) → FileScanResult`

Inspects content and reports whether it may proceed.

- Called **after** type and size validation, **before** the file is written (R-004).
- Must not consume the stream destructively — position is restored before returning, so the caller
  can still persist the content.
- Returns a result, never throws, for a file it simply judges unsafe. Exceptions are reserved for
  scanner malfunction.

## `FileScanResult`

| Member | Meaning |
|--------|---------|
| `IsSafe` | Whether the file may be stored |
| `Reason` | Human-readable explanation, populated when `IsSafe` is false |

## Implementation: `PermissiveFileScanner`

Approves every file that already passed validation. Performs no network calls and reads no external
signature database.

## Guarantees

- Upload is **blocked** when `IsSafe` is false; no file is written and no record is created (FR-009).
- A blocked upload surfaces `Reason` to the user rather than a generic failure.
- The scan step is unconditional — it cannot be bypassed by any caller of `DocumentService`.

## Known Limitation

The default implementation detects nothing. This is a documented training-environment trade-off, must
be stated in the README, and must not be presented as real protection.

## Future: Asynchronous Scanning

The production design — Azure Functions triggered by Queue Storage, scanning out of band — is
recorded as **R-014** in [research.md](../research.md) and is **not implemented**.

Note that it does not fit this synchronous contract. `ScanAsync` returns a verdict *before* the file
is stored; a queue-based scanner returns only an acknowledgement, with the verdict arriving later.
Adopting it therefore requires a second, asynchronous contract and a `ScanStatus` field on
`Document` — not merely a different registration of this interface. The seam still pays off: only
`DocumentService`'s call site and the DI registration change, not the upload pipeline's structure.
