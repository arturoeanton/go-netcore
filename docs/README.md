# goclr documentation

All project documentation lives here. The top-level [`README.md`](../README.md) is
the entry point (what goclr is, highlights, getting started); everything else is
under `docs/`.

## Status & planning

| Doc | What it covers |
| --- | --- |
| [ROADMAP.md](ROADMAP.md) | Milestones (M0–M8) and the done/pending checklist — what is implemented vs outstanding |
| [GAPS.md](GAPS.md) | Gap analysis to a complete product: per-component state, effort, and what blocks the MVP success criteria |
| [LIMITATIONS.md](LIMITATIONS.md) | Known limitations / tracked technical debt — each documented so it fails predictably, not silently |

## Subsystem design notes

| Doc | What it covers |
| --- | --- |
| [REFLECT.md](REFLECT.md) | `reflect` as compile-time type descriptors (the foundation reflection-heavy libraries need) |
| [DESIGN-typed-box.md](DESIGN-typed-box.md) | Per-value runtime type identity (`GoNamed{TypeId, Value}`) — Stringer/`%T`/interface dispatch over representation-sharing named types |
| [DESIGN-callback-bridge.md](DESIGN-callback-bridge.md) | The interface method-callback bridge — calling a Go interface method from a C# shim (`container/heap`, `io.Writer`, `io/fs`) |
| [DESIGN-unsafe-pointer.md](DESIGN-unsafe-pointer.md) | `unsafe.Pointer`: the hard ceiling (no raw memory) and the safe idioms goclr supports (`string↔[]byte`, read-only `reflect.*Header` offsets) |
| [GOJA-STRATEGY.md](GOJA-STRATEGY.md) | Historical: the goja / `unsafe.Pointer` strategy and how it was resolved (kept for context) |

## Conventions

- **Validation targets** (goja, Gin, Echo) drive the roadmap; they are *not* products
  of goclr and earn no special cases in the compiler. The compiler stays
  **application-agnostic**: general Go language + standard-library semantics, with
  projects supplying their own overlays under [`../goclr.overlays/`](../goclr.overlays/)
  for any hard-to-lower vendored dependency.
- Every "done" item is verified **byte-for-byte vs `go run`** (conformance or
  validation) unless explicitly noted.
