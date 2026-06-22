# priorizar — checklist vivo

Leyenda: ✅ hecho · 🟡 parcial · ⬜ pendiente. Cada ✅ se cierra con fixture/validación
byte-exacta vs `go run`, tests verdes y documentación. Ver [VISION.md](VISION.md).

## Orden recomendado (foco actual, serializado — uno a la vez)

1. ✅ `goclr test` compatible con `go test` — tag `0.0.52.goclr-test`
2. ✅ Compatibility report estable HTML/JSON — tag `0.0.53.compat-report`
3. ✅ typed-nil en interfaces (`var p *T; any(p) == nil` ⇒ `false`) — tag `0.0.54.typed-nil-iface`
4. ✅ `reflect.StructField.Name` / `.Tag` directo — tag `0.0.55.reflect-structfield`
5. ⬜ function values de funciones shimmed
6. ⬜ formato de panic no recuperado igual a Go
7. ⬜ deep `reflect` mínimo (más goja/libs)
8. ⬜ `text/template` + `google/uuid` + `errgroup` + testify
9. ⬜ GORM target chico
10. ⬜ performance / AOT

## Lista priorizada completa (50)

1. ✅ `goclr test` compatible con `go test` — facilidad media, impacto altísimo · tag `0.0.52.goclr-test`
2. ✅ Compatibility report estable HTML/JSON — alta, altísimo · tag `0.0.53.compat-report`
3. ⬜ Matriz de cobertura por función stdlib/externa — alta/media, alto
4. ⬜ Errores accionables `GCLR05xx/GCLR07xx` (símbolo, por qué, workaround, build tag, overlay) — alta, alto
5. ✅ typed-nil en interfaces — media, altísimo · tag `0.0.54.typed-nil-iface` (residual documentado: `== nil` del puntero recuperado)
6. ⬜ panic no recuperado con formato Go (`panic:` + goroutine stack) — media, alto
7. ✅ `reflect.StructField.Name` / `.Tag` directo — alta/media, alto · tag `0.0.55.reflect-structfield`
8. ⬜ function values de funciones shimmed (`strings.TrimFunc(s, unicode.IsSpace)`) — media, alto
9. ⬜ `%T` / `%#v` / nil maps más exactos — media, medio/alto
10. ⬜ tipo exacto para composites reflejados dinámicamente — media/difícil, alto
11. ⬜ per-value runtime type tags / itable — difícil, altísimo
12. ⬜ deep `reflect`: `MakeFunc`, `Value`/`Type` profundas — difícil, altísimo
13. ⬜ copiado profundo de arrays `[N]T` dentro de structs — media/difícil, medio/alto
14. 🟡 `sync.Pool` y `sync.Cond` (Pool existe; Cond pendiente) — media, medio/alto
15. 🟡 `regexp` más Go/RE2 exacto — media/difícil, alto
16. 🟡 `time` con zonas locales reales (hoy UTC-only) — media, medio
17. 🟡 Unicode special-casing completo — media, medio
18. ⬜ `text/template` y `html/template` — media/difícil, alto
19. ⬜ `encoding/gob` — difícil, medio/alto
20. ⬜ `archive/zip` / `archive/tar` — media, medio/alto
21. 🟡 `crypto/rsa·ecdsa·x509·tls` full (hoy x509/acme bajado, TLS no-op) — difícil, altísimo
22. ⬜ `net/smtp` — media, medio
23. 🟡 `runtime/debug`, stack traces y metadata — media/difícil, alto
24. ⬜ Portable PDB / posiciones / stack traces — difícil, alto
25. ⬜ `container/ring` — alta, bajo/medio
26. ⬜ `text/scanner` y `text/tabwriter` — media, medio
27. ⬜ `golang.org/x/sync/errgroup` — alta/media, alto
28. ⬜ `google/uuid` — alta, alto
29. ⬜ Testify — media, alto
30. ⬜ JWT libraries — media, alto
31. ⬜ Redis client pure-Go — media/difícil, alto
32. ⬜ WebSocket — media/difícil, alto
33. ⬜ GORM — difícil, altísimo
34. ⬜ gRPC — muy difícil, altísimo
35. ⬜ Typed IL / menos boxing — difícil, alto
36. ⬜ Release optimizations — difícil, alto
37. ⬜ NativeAOT + trimming — difícil, alto/comercial
38. ⬜ Startup / warm JIT razonable — media/difícil, alto
39. ⬜ Incremental cache por módulo — media, alto UX
40. ⬜ `--emit-il/-ir/-ssa`, `--keep-temp`, `--explain` — media, medio/alto
41. 🟡 Bundle final más parecido a `go build` — media, alto
42. 🟡 Reducir build tags especiales (purego/nomsgpack) — media/difícil, alto
43. 🟡 Overlays documentados como "compat layer" (por qué/qué/exactitud/test) — alta, alto
44. 🟡 Detectar native deps problemáticas y explicarlas (quic-go/plugin/cgo/x-sys) — media, alto
45. ⬜ Go plugin (detectar y explicar, no implementar) — muy difícil, bajo/medio
46. ⬜ cgo (estrategia: `CGO_ENABLED=0`/overlays) — muy difícil, variable
47. 🟡 Go assembly (rechazar claro) — muy difícil, bajo/medio
48. ⬜ Scheduler Go completo (no se hará; ThreadPool alcanza) — muy difícil, medio
49. ⬜ Netpoller estilo Go — muy difícil, medio/alto
50. ⬜ GC/runtime Go real (no es el camino) — casi inviable, bajo

## Bitácora de cierres

- ✅ **#1 `goclr test`** — tag `0.0.52.goclr-test`. Overlay real-Go `testing` +
  `testing/internal/testdeps` (`internal/frontend/overlays/testing`), lowered vía
  `compileFromSource`; `go/test`'s generated `_testmain.go` corre tal cual
  (`MainStart`/`M.Run`). FailNow/SkipNow vía panic/recover. `goclr test ./pkg` compila los
  tests a .NET y los corre (TestXxx, subtests, Fatal/Skip), exit 0/1. Validado en
  `tests/gotest`. Fix de fondo: `frontend.Load` ahora dedupea la recursión por package ID
  (no import path), así el test-variant `pkg [pkg.test]` no se pierde frente al `pkg` plano.
- ✅ **#2 Compatibility report HTML/JSON** — tag `0.0.53.compat-report`. `goclr analyze`
  gana `--html` (reporte autocontenido, inline CSS, badges por estado) + `-o <file>`; el
  JSON gana un `summary` estable (counts OK/WARN/FAIL + cobertura stdlib full/partial/pending).
  `internal/analysis/html.go` + tests. Package-by-package, accionable y publicable como artefacto.
- ✅ **#3 typed-nil en interfaces** — tag `0.0.54.typed-nil-iface`. `exprCoerced` boxea un
  puntero concreto nil en un `GoPtr{Value:null, TypeId}` (no-null) vía `Rt.BoxNilPtr`, así
  `var p *T; any(p) == nil` es `false` (el gotcha clásico `err != nil` ahora es fiel) y
  assert/dispatch resuelven el tipo dinámico. Un solo punto de contacto (bajo riesgo),
  conformance 205 verde + echo compila. Fixture 404. Residual documentado en LIMITATIONS:
  el `== nil` del puntero recuperado de la interfaz (compara identidad, no payload).
- ✅ **#4 `reflect.StructField.Name`/`.Tag`** — tag `0.0.55.reflect-structfield`. Estaba
  marcado pendiente pero ya andaba casi todo; cerrados dos gaps reales: (a) `f.Type.Name()`
  de un campo de tipo básico daba `""` (faltaba setear `entry.name` para `*types.Basic` en
  el builder de descriptors → ahora "string"/"int"), y (b) `Type.FieldByName` panicaba
  (faltaba el shim → agregado `Reflect.Type_FieldByName` devolviendo `(StructField, bool)`).
  Fixture 405 byte-exacto (tags json/xml/validate, anónimos, PkgPath, Lookup, FieldByName).
