# goclr.overlays

Project-supplied source overlays for **vendored dependencies**. When a third-party
package a project depends on uses a construct goclr cannot lower (most commonly
`unsafe.Pointer` byte↔numeric reinterpretation), the project places a goclr-safe
rewrite of the offending file here and goclr swaps it into `vendor/` before the
type-checker and backend ever see the original.

The compiler is **agnostic** to which dependencies these are: `frontend.ApplyOverlays`
applies whatever it finds under this directory. Adding support for a new dependency
is purely a matter of dropping a file here — no compiler change.

## Layout

Mirror the import path, with a `.go.txt` suffix (so the files are not themselves
compiled by `go build ./...`):

```
goclr.overlays/<import/path>/<file>.go.txt   →   vendor/<import/path>/<file>.go
```

Overlays apply only when the module is vendored (`go mod vendor`) and the target
file already exists in `vendor/`; otherwise they are silently skipped.

## What lives here today

The overlays for [goja](https://github.com/dop251/goja) and its
[regexp2](https://github.com/dlclark/regexp2) dependency. goja is **not** a product
of goclr — it is a deliberately hard validation target: a real, large dependency
that forces goclr to implement general Go features correctly. Its overlays replace
`unsafe.Pointer` reinterpretation with the equivalent `encoding/binary` access,
which is byte-for-byte identical and fully supported by the backend.

Distinct from these are goclr's **standard-library** source overlays (e.g. a
reflectlite-free `sort`), which are part of the compiler itself and embedded in the
binary — they apply to every program and are not application-specific.
