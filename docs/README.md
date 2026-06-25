# goclr documentation

All project documentation lives here. The top-level [`README.md`](../README.md) is the
entry point — what goclr is, the highlights, and the **[Quick Start](../README.md#quick-start)**.
Everything below goes deeper.

## Recommended reading order

1. **[../README.md](../README.md)** — what goclr is, the highlights, and the Quick Start.
2. **[QUICKSTART.md](QUICKSTART.md)** — the extended, troubleshooting-oriented setup walkthrough.
3. **[ROADMAP.md](ROADMAP.md)** — what is implemented vs. in progress (the done/pending checklist).
4. **[LIMITATIONS.md](LIMITATIONS.md)** — the deliberately-deferred edges, each documented to fail predictably.
5. The subsystem design notes below, as needed (read [DESIGN-typed-box.md](DESIGN-typed-box.md) before [REFLECT.md](REFLECT.md)).

## Getting started

| Doc | What it covers |
| --- | --- |
| [QUICKSTART.md](QUICKSTART.md) | Clone → build → run, the runtime-DLL build, vendoring, and a troubleshooting table |

## Status & planning

| Doc | What it covers |
| --- | --- |
| [ROADMAP.md](ROADMAP.md) | Milestones (M0–M8) and the done/pending checklist — what is implemented vs outstanding |
| [GAPS.md](GAPS.md) | Gap analysis to a complete product: per-component state, effort, and the distance to GORM/KrakenD/Fiber/AOT |
| [LIMITATIONS.md](LIMITATIONS.md) | Known limitations / tracked technical debt — each documented so it fails predictably, not silently |
| [COVERAGE.md](COVERAGE.md) | Per-package standard-library coverage matrix (from `goclr coverage`) |
| [VISION.md](VISION.md) | The prioritized vision and the guiding principles (agnostic, no technical debt, transparency) — *in Spanish* |
| [priorizar.md](priorizar.md) | The living roadmap checklist with tagged closures — *in Spanish* |

## Subsystem design notes

| Doc | What it covers |
| --- | --- |
| [REFLECT.md](REFLECT.md) | `reflect` as compile-time type descriptors (the foundation reflection-heavy libraries need) |
| [DESIGN-typed-box.md](DESIGN-typed-box.md) | Per-value runtime type identity (`GoNamed{TypeId, Value}`) — Stringer/`%T`/interface dispatch over representation-sharing named types |
| [DESIGN-callback-bridge.md](DESIGN-callback-bridge.md) | The interface method-callback bridge — calling a Go interface method from a C# shim (`container/heap`, `io.Writer`, `io/fs`) |
| [DESIGN-unsafe-pointer.md](DESIGN-unsafe-pointer.md) | `unsafe.Pointer`: the hard ceiling (no raw memory) and the safe idioms goclr supports (`string↔[]byte`, read-only `reflect.*Header` offsets) |
| [GOJA-STRATEGY.md](GOJA-STRATEGY.md) | Historical: the goja / `unsafe.Pointer` strategy and how it was resolved (kept for context) |

## Conventions

- **Validation targets** (goja, Gin, Echo) drive the roadmap; they are *not* products of
  goclr and earn no special cases in the compiler. The compiler stays
  **application-agnostic**: general Go language + standard-library semantics, with
  projects supplying their own overlays under [`../goclr.overlays/`](../goclr.overlays/)
  for any hard-to-lower vendored dependency.
- Every "done" item is verified **byte-for-byte vs `go run`** (conformance or validation)
  unless explicitly noted.
- Some planning docs ([VISION.md](VISION.md), [priorizar.md](priorizar.md)) are written in
  Spanish; the rest of the documentation is in English.
</content>
