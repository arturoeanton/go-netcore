# priorizar — checklist vivo

Leyenda: ✅ hecho · 🟡 parcial · ⬜ pendiente. Cada ✅ se cierra con fixture/validación
byte-exacta vs `go run`, tests verdes y documentación. Ver [VISION.md](VISION.md).

## Orden recomendado (foco actual, serializado — uno a la vez)

1. ⬜ `goclr test` compatible con `go test`
2. ⬜ Compatibility report estable HTML/JSON
3. ⬜ typed-nil en interfaces (`var p *T; any(p) == nil` ⇒ `false`)
4. ⬜ `reflect.StructField.Name` / `.Tag` directo
5. ⬜ function values de funciones shimmed
6. ⬜ formato de panic no recuperado igual a Go
7. ⬜ deep `reflect` mínimo (más goja/libs)
8. ⬜ `text/template` + `google/uuid` + `errgroup` + testify
9. ⬜ GORM target chico
10. ⬜ performance / AOT

## Lista priorizada completa (50)

1. ⬜ `goclr test` compatible con `go test` — facilidad media, impacto altísimo
2. ⬜ Compatibility report estable HTML/JSON — alta, altísimo
3. ⬜ Matriz de cobertura por función stdlib/externa — alta/media, alto
4. ⬜ Errores accionables `GCLR05xx/GCLR07xx` (símbolo, por qué, workaround, build tag, overlay) — alta, alto
5. ⬜ typed-nil en interfaces — media, altísimo
6. ⬜ panic no recuperado con formato Go (`panic:` + goroutine stack) — media, alto
7. ⬜ `reflect.StructField.Name` / `.Tag` directo — alta/media, alto
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

_(se anota aquí cada ✅ con su tag, en orden de cierre)_
