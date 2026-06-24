# priorizar — checklist vivo

Leyenda: ✅ hecho · 🟡 parcial · ⬜ pendiente. Cada ✅ se cierra con fixture/validación
byte-exacta vs `go run`, tests verdes y documentación. Ver [VISION.md](VISION.md).

## Orden 2 (nuevo foco, serializado — uno a la vez)

1. ✅ Testify mínimo — tag `0.0.64.testify` (`goclr test` + testify/assert corren en el CLR)
2. ✅ JWT library — tag `0.0.109.jwt-parse`: golang-jwt/v5 firma **y verifica** HS256 end-to-end
   byte-exacto (sign + Parse + claims + rechazo de key incorrecta). Cerró: json.Unmarshal a un
   map de campo de struct (`&token.Header` escribía el Value sin usar FSet → mapa vacío),
   reuso/merge de un map target no-nil (jwt aliasa un MapClaims por `token.Claims` + copia local),
   y firma de `json.Number.Float64/Int64/String` (receiver string → param GoString). Fixture 453.
3. ✅ text/template + html/template — tags `0.0.91`–`0.0.97` (motor real: parse+exec, escaping
   contextual, safe types, contextos URL/JS/CSS/event/style). Fixtures 436–441.
4. ✅ per-function stdlib coverage matrix — tag `0.0.110.coverage-matrix` (`goclr coverage` [--gap/--missing/--json]; cruza la API exportada de cada paquete (go/packages) contra los registros de shims + compileFromSource; total 51% / 1853 de 3632 símbolos en 95 paquetes; snapshot en docs/COVERAGE.md)
5. ⬜ GORM mini target con una sola entidad
6. ⬜ bench/startup report
7. ⬜ NativeAOT smoke

## Orden recomendado (foco previo, serializado — uno a la vez)

1. ✅ `goclr test` compatible con `go test` — tag `0.0.52.goclr-test`
2. ✅ Compatibility report estable HTML/JSON — tag `0.0.53.compat-report`
3. ✅ typed-nil en interfaces (`var p *T; any(p) == nil` ⇒ `false`) — tag `0.0.54.typed-nil-iface`
4. ✅ `reflect.StructField.Name` / `.Tag` directo — tag `0.0.55.reflect-structfield`
5. ✅ function values de funciones shimmed — tag `0.0.56.shim-func-value`
6. ✅ formato de panic no recuperado igual a Go — tag `0.0.57.panic-format`
7. ✅ deep `reflect` mínimo (`MakeFunc`/`Value.Call`/`Method.Call`) — tag `0.0.58.reflect-deep`
8. 🟡 `text/template` + `google/uuid`(✅ `0.0.61`) + `errgroup`(✅ `0.0.59`) + testify(✅ `0.0.64.testify`) — solo text/template pendiente (stub grande)
9. 🟡 GORM target chico — DISTANCIA MEDIDA (tag `0.0.62.gorm-distance`): no hay gap de
   compilador; es una cadena de shims (time ✓, runtime caller ✓ stub, slog handler pendiente)
   + necesita dialector/driver pure-Go. Esfuerzo multi-paso, no un cierre único.
10. 🟡 performance / AOT — EVALUADO (tag `0.0.63.perf-assess`): startup chico ~20ms (bien),
    goja ~3.2s (JIT-bound); ReadyToRun es la palanca real (necesita publish project); AOT
    inviable sin rework (reflection en el mecanismo de shims); typed-IL/menos-boxing = lever
    de throughput. configProperties (TieredPGO off) para favorecer startup de programas cortos.
    Documentado en GAPS.md.

## Lista priorizada completa (50)

1. ✅ `goclr test` compatible con `go test` — facilidad media, impacto altísimo · tag `0.0.52.goclr-test`
2. ✅ Compatibility report estable HTML/JSON — alta, altísimo · tag `0.0.53.compat-report`
3. ✅ Matriz de cobertura por función stdlib/externa — alta/media, alto · tag `0.0.110.coverage-matrix`
4. ⬜ Errores accionables `GCLR05xx/GCLR07xx` (símbolo, por qué, workaround, build tag, overlay) — alta, alto
5. ✅ typed-nil en interfaces — media, altísimo · tag `0.0.54.typed-nil-iface` (residual documentado: `== nil` del puntero recuperado)
6. ✅ panic no recuperado con formato Go (`panic:` + goroutine stack) — media, alto · tag `0.0.57.panic-format`
7. ✅ `reflect.StructField.Name` / `.Tag` directo — alta/media, alto · tag `0.0.55.reflect-structfield`
8. ✅ function values de funciones shimmed (`strings.TrimFunc(s, unicode.IsSpace)`) — media, alto · tag `0.0.56.shim-func-value`
9. ✅ `%T` / `%#v` / nil maps más exactos — media, medio/alto · tags `0.0.105`–`0.0.108` (identidad Stringer en elementos de slice/array/map, campos de struct y claves de map; `%T` de puntero nil+no-nil para todo pointee; `%#v` de campo slice con tipo de elemento exacto)
10. ⬜ tipo exacto para composites reflejados dinámicamente — media/difícil, alto
11. ⬜ per-value runtime type tags / itable — difícil, altísimo
12. ✅ deep `reflect` mínimo: `MakeFunc`, `Value.Call`, `Method.Call` — difícil, altísimo · tag `0.0.58.reflect-deep` (reflejar métodos de deps grandes queda fuera de scope)
13. ⬜ copiado profundo de arrays `[N]T` dentro de structs — media/difícil, medio/alto
14. 🟡 `sync.Pool` y `sync.Cond` (Pool existe; Cond pendiente) — media, medio/alto
15. 🟡 `regexp` más Go/RE2 exacto — media/difícil, alto
16. 🟡 `time` con zonas locales reales (hoy UTC-only) — media, medio
17. 🟡 Unicode special-casing completo — media, medio
18. ✅ `text/template` y `html/template` — media/difícil, alto · tags `0.0.91`–`0.0.97`
19. ⬜ `encoding/gob` — difícil, medio/alto
20. ⬜ `archive/zip` / `archive/tar` — media, medio/alto
21. 🟡 `crypto/rsa·ecdsa·x509·tls` full (hoy x509/acme bajado, TLS no-op) — difícil, altísimo
22. ⬜ `net/smtp` — media, medio
23. 🟡 `runtime/debug`, stack traces y metadata — media/difícil, alto
24. ⬜ Portable PDB / posiciones / stack traces — difícil, alto
25. ✅ `container/ring` — alta, bajo/medio · tag `0.0.60.container-ring` (compila de source)
26. ⬜ `text/scanner` y `text/tabwriter` — media, medio
27. ✅ `golang.org/x/sync/errgroup` — alta/media, alto · tag `0.0.59.errgroup` (compila de source + corre; agregado `context.WithCancelCause`/`Cause`)
28. ✅ `google/uuid` — alta, alto · tag `0.0.61.uuid` (compila de source + corre; cerró os.Getuid, net.Interfaces, bytes.EqualFold, hex.Encode/Decode, bridge io.Reader)
29. ✅ Testify — media, alto · tag `0.0.64.testify` (assert.* corre bajo `goclr test`; cerró safe tag, hex.Dump, Value.CanConvert, StructField.IsExported, regexp.Match, os.Lstat, runtime.Goexit)
30. ✅ JWT libraries — media, alto · tag `0.0.109.jwt-parse` (golang-jwt/v5 sign+verify HS256 end-to-end)
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
- ✅ **#5 function values de funciones shimmed** — tag `0.0.56.shim-func-value`. La mayoría
  ya andaba (`Closures.FromShim` envuelve el extern); cerrado el gap variádico: una func
  shim variádica como valor (`fmt.Sprintf`) no empaquetaba sus args trailing en el `GoSlice`
  del parámetro → `InvokeShim` ahora detecta el último parámetro `GoSlice` y empaqueta la
  cola (salvo que el caller ya pasara el slice). Fixture 406 byte-exacto (valor, arg, slice,
  callback unicode, variádica, method value de tipo shim).
- ✅ **#6 panic no recuperado formato Go** — tag `0.0.57.panic-format`. Un entry sintético
  (`__goclr_entry`) corre `init()`+`main()` dentro de try/catch(GoPanicException) →
  `Rt.FatalPanic`, que imprime `panic: <value>` + `goroutine 1 [running]:` + frames y sale
  con exit 2 (antes: volcado .NET "Unhandled exception", exit 255). Recuperados intactos
  (path de defer/recover no tocado). Frames = stack CLR (no hay metadata de stack Go).
  Conformance verde (wrapper transparente en happy path). Test `tests/panicfmt`.
- ✅ **#7 deep reflect mínimo** — tag `0.0.58.reflect-deep`. `Value.Call` ya andaba.
  **MakeFunc**: devuelve un adapter con la convención raw (`func(int,int)int`) que empaqueta
  los args en `[]reflect.Value`, invoca el closure usuario `func([]Value)[]Value` y
  desempaqueta el resultado (single/multi/void) — sirve a `.Interface()` y a `.Call`.
  **Value.Method/MethodByName(...).Call**: bound-method Value vía el callback-bridge;
  `collectReflectMethods` registra adapters por método. **GOTCHA: registrar TODOS los
  métodos de TODO el closure rompió goja** (reflect-heavy, miles de tipos → assembly corrupto
  `BadImageFormatException`); acotado a tipos del **paquete main** (`named.Obj().Pkg()==c.root`)
  — caso común (validators/ORM sobre structs propios), goja vuelve a correr. Fixture 407.
- ✅ **#8/#27 errgroup** — tag `0.0.59.errgroup`. `golang.org/x/sync/errgroup` compila de
  source y corre (goroutines concurrentes + propagación del primer error). Gap cerrado:
  `context.WithCancelCause`/`context.Cause` (+ `GoContext.CauseVal`/`CancelCause`).
  `examples/demo_errgroup` byte-exacto. Resto de #8 pendiente: text/template (stub no-op
  grande), google/uuid + testify (no vendored).
- ✅ **#25 container/ring** — tag `0.0.60.container-ring`. Agregado a `compileFromSource`
  (Go puro: lista circular con punteros, sin deps). Fixture 408 byte-exacto (New/Next/Prev/
  Move/Do/Len/Link). Conformance verde.
- ✅ **#8/#28 google/uuid** — tag `0.0.61.uuid`. `github.com/google/uuid` compila de source
  y corre (v4 con rander custom, v5 NewSHA1, parse/format, version/variant, nil). Cadena de
  gaps cerrada: `os.Getuid`/`Getgid`/`Getppid`, `net.Interfaces` (lista vacía — sin
  enumeración de NICs), `bytes.EqualFold`, `encoding/hex.Encode`/`Decode`, y el **bridge de
  io.Reader** (`io.ReadFull` dispara el `Read` del reader de usuario — simétrico a io.Writer;
  `io.Reader` agregado a `bridgeInterfaces`). `examples/demo_uuid` byte-exacto. Fixture 409
  (bytes.EqualFold + hex.Encode/Decode + io.ReadFull con reader de usuario). Conformance verde.
- 🟡 **#9 GORM (distancia medida)** — tag `0.0.62.gorm-distance`. Compilé
  `gorm.io/gorm/schema.Parse` por goclr: NO hay gap de compilador, es una cadena de shims.
  Cerrados y commiteados (útiles standalone): `time.Time.Date`/`Clock`/`AddDate` (jinzhu/now)
  + `runtime.Callers`/`CallersFrames`/`Frames.Next`/`runtime.Frame` (stub — sin metadata de
  stack Go; gorm loguea SQL sin file:line, lo tolera). Próximo muro: handler `log/slog` de
  gorm; y para ORM completo falta dialector/driver pure-Go. Fixture 410 (time methods).
  Documentado en GAPS.md. Esfuerzo multi-paso, no cierre único.
- 🟡 **#10 performance / AOT (evaluado)** — tag `0.0.63.perf-assess`. Medido: hello-world
  ~20ms (startup ya bueno), goja (~15MB) ~3.2s (JIT-bound). Palancas + distancia documentadas
  en GAPS.md: **ReadyToRun** (crossgen vía publish project) = mayor win de startup para
  programas grandes; **NativeAOT inviable sin rework** (el runtime de shims usa reflection:
  InvokeShim/MethodInfo.Invoke, scan de `[GoShim]`, reflect — el trimming los rompe);
  **typed-IL/menos-boxing** = lever de throughput (reescritura de backend). Mejora concreta:
  `configProperties` con TieredPGO=false (favorece startup de programas cortos; servidores
  apenas lo notan). Linker test verde, JSON válido.
- ✅ **#29 testify** — tag `0.0.64.testify`. `github.com/stretchr/testify/assert` corre en el
  CLR bajo `goclr test`: Equal (incl. struct deep-equal), NotEqual, Greater, Len, ElementsMatch,
  Contains (string/map), NoError/Error/Nil + los mensajes de fallo ricos de testify. Cadena de
  gaps cerrada (útiles standalone): build tag **`safe`** (path no-unsafe de go-spew),
  `encoding/hex.Dump`, `reflect.Value.CanConvert`, `reflect.StructField.IsExported`,
  `(*regexp.Regexp).Match`, `os.Lstat`, `runtime.Goexit` (abort coarse). `examples/demo_testify`
  (PASS + el path de FALLO con exit 1 verificado). Conformance + echo verdes.
- 🟡 **#2/#30 JWT (firma HS256)** — tag `0.0.65.jwt-signs`. `golang-jwt/v5` compila y **firma
  HS256** byte-equivalente. **Bug general arreglado**: `shimMethodValue` no boxeaba un receiver
  value-type (ej. `crypto.Hash`=uint) al capturarlo en el `object[]` del closure → NRE; ahora
  `emitBox(recvType)`. Gaps cerrados (útiles standalone): `math.Modf`, `json.Number`
  (Float64/Int64/String), `encoding/hex.Dump`, `big.Int.FillBytes`, `base64.Encoding.Strict`,
  `math/big`... + stubs **fail-closed** de crypto asimétrico (ecdsa/rsa/ed25519 Sign/Verify,
  x509.Parse*PublicKey/DecryptPEMBlock) para que jwt compile (ES*/RS*/PS*/EdDSA rechazan, nunca
  aceptan mal). **Pendiente**: `jwt.Parse` falla leyendo el header ("alg unspecified") — debug
  del round-trip json del header. `examples/demo_jwt`.
- 🟡 **Fiber (distancia medida)** — tag `0.0.66.fiber-distance`. `gofiber/fiber/v2` corre sobre
  **fasthttp** (HTTP propio, no net/http) → campaña fasthttp, no target rápido. El código de
  fiber compila tras cerrar gaps generales útiles: **overlay `testing` ahora siempre activo**
  (fiber/utils importa testing en código normal), `os.Args` shimeado + **fix general: shimVars
  value-type ahora unboxean** (os.Args daba InvalidProgram), `text/tabwriter` compila-de-source
  (dead code al servir; runtime-crashea si se usa — documentado). Muro: deps de fasthttp —
  `andybalholm/brotli` pega un gap de lowering (`s[i].a.b = v`, asignación a campo anidado de
  elemento de slice) + fasthttp es unsafe-heavy. Documentado en GAPS.md.
- 🟢 **Fiber — destrabe ronda 2** — tag `0.0.67.fiber-compress-shims`. Cerrados como features/shims
  generales (cada uno con fixture standalone, byte-exact vs `go run`):
  - `s[i].a.b = v` (escritura a campo anidado de elemento de slice/array, incl. `*[N]T`) — fixture 411.
  - **`range` sobre `*[N]T`** (puntero a array) — feature Go que faltaba; deref al slice y rangea — fixture 412.
  - `encoding/binary.ReadUvarint`/`ReadVarint` sobre cualquier `io.ByteReader` (readers shim directo,
    reader de usuario vía callback-bridge) — fixture 413.
  - `math/bits.Reverse8`/`Reverse16`; `hash/crc32.Update`/`NewIEEE`/`IEEETable` (CRC de gzip) — fixture 414.
  Próximo muro: **`io.MultiWriter`** (gzip `WriteTo`) — requiere dispatch `io.Writer` heterogéneo
  desde un shim (digest `GoHash32` + writer de usuario en una llamada); fix limpio = bajar
  `io.MultiWriter` desde fuente Go real. Diferido como tarea propia. Detalle en GAPS.md.
- 🟢 **Muro unsafe de fasthttp MEDIDO — NO existe.** Medición directa vs `fasthttp@v1.51.0` + todo
  el árbol: fasthttp core = 4 sitios unsafe (todos idioms seguros bytes↔string que goclr baja en
  go1.20+; la forma dura `reflect.SliceHeader`-write está tras `!go1.20`), brotli = 0, klauspost = 0
  activos (los 6 `unsafe.Pointer` son código muerto comentado), asm = solo `matchlen` con fallback
  puro-Go auto-seleccionado + `cpuid` que no se importa, syscall = trivial (`ECONNRESET`), reflect = 0.
  `goclr analyze` → todo el árbol compatible. **Veredicto: fiber es un target tratable** (long-tail
  de stdlib como gin/echo, empezando por io.MultiWriter), NO un callejón sin salida. Detalle en GAPS.md.
