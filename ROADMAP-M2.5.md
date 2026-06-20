# M2.5 — Standard Library Overlay: Completion & Gaps

This completes the per-package M2.5 plan with the pieces that plan assumes but
does not pin down, plus the packages and ecosystem tracks needed for the three
real target classes: **business apps, games, and SaaS**.

Read this *alongside* the per-package roadmap. The per-package list (errors,
strconv, strings, bytes, unicode, math, sort, io, bufio, context, sync, time,
runtime, fmt, json, reflect, regexp, os, path, log, net/url, mime, net/http,
Echo, database/sql, encoding, crypto, compress, net, testing) is correct and
stays as written. This document adds: the delivery mechanism, the missing
packages, the corrections, the third-party ecosystem, the games fork, the
cross-cutting semantic hazards, and a target-class priority matrix.

Legend: `compile-direct` · `overlay` (Go source w/ `//go:build goclr`) · `shim`
(Go API → .NET) · `stub` · `blocked`.

---

## Progress (live)

**88 conformance fixtures pass, all byte-exact vs `go run`. P0 is complete.**

Foundations (§0.1) — **DONE**: multi-package lowering (main + transitive non-stdlib
closure → one assembly), package-level vars + `init()` (`__goclr_init`), the C# shim /
extern-ref mechanism (dynamic MemberRef/TypeRef/AssemblyRef beyond the fixed token
spine; `GoCLR.Stdlib.dll` 2nd managed assembly; linker copies it; variadic +
multi-result `object[]` shims), the **opaque value-type pattern** (sync.\*/time.Time/
strings.Builder/bytes.Buffer as runtime handles, `&v` shares the handle), and **shim
variables** (`os.Stdout`/`Stderr`/`time.UTC`).

Reflect keystone (§5) — **read-path DONE**: reflection in C# over boxed values + a
compiler-emitted struct-tag registry; powers `fmt %v/%+v`, `json.Marshal`.

Native function values (§) — **DONE**: GoClosure gains a native-delegate fallback in
the dispatcher, so the runtime/stdlib can hand Go callable function values
(context.CancelFunc, future time.AfterFunc/sync.OnceFunc).

json write-path (§) — **DONE via descriptors**: `json.Unmarshal` decodes into
structs (nested + slice-of-struct), slices, maps, primitives, and `interface{}`,
using a compiler-emitted type descriptor (the runtime erases slice/map element
types) and writing back through the GoPtr cell.

P0 packages shimmed (byte-exact): `math`, `strings` (+`Builder`), `bytes` (+`Buffer`),
`errors`, `unicode`, `unicode/utf8`, `strconv`, `math/bits`, `os`, `reflect`,
`encoding/json` (Marshal + Unmarshal), `fmt` (+`Fprint*`), `io` (`WriteString`),
`sort`, `sync` (Mutex/RWMutex/WaitGroup/Once/Map), `time` (Duration +
`time.Time`/`Format`), `math/rand` (seeded deterministic), `context`
(Background/TODO/WithValue/WithCancel/WithTimeout + Done/Err/Value). String
conversions and the `error` model (IGoError fallback) done.

Float ftoa parity (§0.4) — **DONE**: a shortest-round-trip formatter (`GoFtoa`)
drives `%v`/`%g`/`%e`/`%E`/`println`/`strconv.FormatFloat`, matching Go's
`exp<-4 || exp>=6` layout, lowercase `e`, and ≥2-digit signed exponent.

reflect write-path (§5 phase 3) — **DONE**: settable `reflect.Value` over the GoPtr
cell — `ValueOf(&x).Elem()` + `Set`/`SetInt`/`SetUint`/`SetFloat`/`SetBool`/
`SetString`, settable struct `Field(i)` (threading writes back through parent
structs), `CanSet`/`CanAddr`, and `reflect.New`.

**P0 is complete.** Next: M3 (goja). Remaining tracks are P1+ (net/http+Kestrel,
net TCP/UDP, crypto/bcrypt+jwt, database/sql, regexp, io/bufio, log/slog, …) per
the priority matrix below.

Documented limitations: `time.Time` is UTC-only (use `.UTC()` for determinism;
Go uses Local); a named numeric type with `String()` (e.g. `time.Duration`) passed to
`fmt` as `any` prints the raw value — call `.String()` (boxed-Stringer pending).

---

## 0. Foundations that must exist BEFORE per-package work

These are not packages — they are the machinery the whole overlay depends on.
None of the per-package work is reproducible without them.

### 0.1 Overlay resolution mechanism (the missing "how")

The plan classifies each package but never says how a Go `import` resolves to the
right implementation. Build it first:

1. **compile-direct detection.** A package compiles directly iff its (transitive,
   for-this-platform) source uses no `syscall`, no assembly, no `unsafe` beyond
   approved patterns, and no `runtime`-internal linknames. Add an analyzer pass
   (extend `internal/analysis`) that classifies each import as
   direct/overlay/shim/blocked and prints a report. This already half-exists
   (cgo/asm/unsafe detection) — formalize it into the resolver.
2. **overlay source tree.** A parallel module (`overlay/`) holding Go files
   tagged `//go:build goclr` that *replace* specific stdlib files. The frontend
   loader must honor a `goclr` build tag and prefer overlay files over the real
   stdlib for the same import path. Decide: file-level replacement (GOFLAGS
   `-overlay` JSON, like `go build -overlay`) vs. whole-package shadowing. The
   `-overlay` JSON map is the lowest-risk: it's a built-in `go/packages` feature.
3. **C# shim manifest.** A table mapping import path + symbol → a
   `GoCLR.Stdlib` type/method, for packages implemented in C# (math, sync,
   atomic, time, os, crypto hashes, compress, sql drivers). Lowering consults
   the manifest: a call to `math.Sqrt` emits a MemberRef to
   `GoCLR.Stdlib.Math.Sqrt` instead of lowering a Go body. This is the same
   token-spine machinery already used for the runtime (Builtins/GoStrings/…),
   generalized and data-driven.
4. **precedence + per-symbol override.** direct < overlay < shim. A package can be
   mostly compile-direct with a few symbols shimmed (e.g. `strings` pure-Go but
   `strings.Builder` shimmed). The resolver must work per-symbol, not just
   per-package.

Ship `GoCLR.Stdlib.dll` as a second managed assembly next to `GoCLR.Runtime.dll`
(the linker already knows how to copy one; generalize to N).

### 0.2 `//go:embed` (compiler feature, not a package)

`embed` is a compiler directive, not a normal import. SaaS embeds web assets,
migrations, templates; business apps embed report templates; games embed assets.
The compiler must, at build time, read the named files and emit their bytes as a
data blob, exposing `embed.FS` / `string` / `[]byte`. This is real compiler work
(M2.5 foundational), not stdlib. Without it, a large fraction of modern repos
won't build.

### 0.3 Build tags + GOOS/GOARCH

Decide and freeze: `GOOS=clr` (or keep a known value like `linux` to satisfy
build constraints that gate asm vs pure-Go?), `GOARCH`. Many crypto/x-sys
packages select a pure-Go path under build tags like `!amd64` or `purego`;
choosing GOARCH wrong forces the asm path → blocked. Likely: present as a generic
arch so packages fall back to their pure-Go implementations, plus a `goclr` tag
for our overlays. This single decision unblocks a surprising number of packages.

### 0.4 Cross-cutting semantic-parity hazards (plan these now, not after)

These bite every formatting/serialization/test path. They are the difference
between "compiles" and "byte-identical to `go run`":

- **Float shortest-round-trip formatting.** Go's `strconv.FormatFloat(g, -1)` uses
  Ryū/shortest-decimal. .NET `"R"`/`"G17"` differ. `fmt`, `json`, `strconv`,
  `println` of floats, `time` all depend on this. Port Go's `strconv` ftoa
  (compile-direct the pure-Go `strconv/ftoa.go` path) rather than shim — it's the
  only way to match. Currently float `println` is best-effort; this closes it.
- **`time` reference layout.** Format/Parse use the `2006-01-02 15:04:05` magic
  reference. Port Go's `time/format.go` (pure Go) rather than map to .NET format
  strings — semantics (timezones, fractional secs, `MST`) won't match otherwise.
- **`json.Marshal` map key ordering.** Go sorts map keys; .NET Dictionary does
  not. Replicate the sort, or json diffs forever. Also struct field order = source
  order.
- **`range` over map order is randomized in Go.** Tests that print map ranges must
  stay order-independent (already the convention) — document it for overlay tests.
- **Integer wraparound / untyped-const overflow** must match two's-complement Go.
  (Already correct for the ops; re-verify for shifts ≥ width and const folding.)
- **Error-string parity.** nil-map write, nil deref, type-assert failure,
  divide-by-zero, index-out-of-range — real code does `err.Error() == "..."` and
  string-matches panics. Mirror Go's exact messages.
- **Goroutine scheduling / fairness / GOMAXPROCS / select fairness.** .NET
  thread-pool ≠ Go scheduler. Mostly fine, but tight spin loops, `runtime.Gosched`,
  and unbuffered-channel fairness can diverge. Document and provide
  `GOMAXPROCS`/`Gosched` shims.
- **`runtime.SetFinalizer` / weak references / `runtime.KeepAlive`.** Some libs
  (sql drivers, buffers, cgo-free pools) rely on finalizers. Map to .NET
  finalizers/`ConditionalWeakTable`; document GC-timing differences.
- **`hash/maphash` + map seed randomization.** Security-sensitive libs seed maps;
  maphash must be a real hash. Shim to a fast managed hash.

### 0.5 Tooling: coverage matrix + `go test` parity (formalize)

- **Per-function coverage matrix.** A generated table (covered / stub / blocked)
  per package symbol, kept in the repo and checked in CI. The plan mentions it;
  make it a real artifact driven by the resolver + tests.
- **`goclr test` ≡ `go test`.** Extend the conformance harness from
  single-`main` programs to running real package test suites under both Go and
  goclr and diffing. This is how you certify `testify`/`GORM`/`net/http`.

---

## 1. Missing CORE packages (add to M2.5A/B)

| Package | Class | Why it's missing-critical |
| --- | --- | --- |
| **`math/rand` + `math/rand/v2`** | shim/overlay | **Games** (everything), ids, jitter, sampling, shuffles. The single biggest core omission. Note: Go's rand is *deterministic* given a seed — port the pure-Go generator so seeded sequences match, don't shim to `System.Random`. |
| **`math/bits`** | shim | bit twiddling used by hashing, compression, bitsets, games. `LeadingZeros`, `OnesCount`, `RotateLeft`, etc. → `System.Numerics.BitOperations`. |
| **`math/big`** | overlay/shim | financial decimals, crypto, some business math. `System.Numerics.BigInteger` covers Int; `big.Float`/`big.Rat` need more care. |
| **`hash`, `hash/fnv`, `hash/crc32`, `hash/crc64`, `hash/adler32`, `hash/maphash`** | overlay/shim | caching, sharding, bloom filters, checksums, ETags. fnv & crc32 are everywhere. |
| **`container/heap`, `container/list`, `container/ring`** | compile-direct | priority queues / schedulers / LRU — common in games and SaaS. Pure Go, should just compile. |
| **`io/fs`, `io/ioutil`, `testing/fstest`** | overlay | `fs.FS` is the abstraction `embed` and filesystem code use. ioutil deprecated but still in the wild. |
| **`flag`, `text/tabwriter`** | compile-direct/overlay | CLIs and tools. |
| **`runtime/debug`** | shim | `debug.Stack()`/`PrintStack()` used inside recover handlers (SaaS error reporting), `ReadBuildInfo`, `SetGCPercent`. |
| **`os/signal`** | shim — **correct the "blocked"** | **graceful shutdown** (SIGINT/SIGTERM) is mandatory for any service/SaaS. Map `signal.Notify` for INT/TERM to .NET `PosixSignalRegistration` / `Console.CancelKeyPress`. Full signal set stays stub. |
| **`os/exec`** | shim | subprocesses (business tools, build pipelines, image/video processing wrappers) → `System.Diagnostics.Process`. |
| **`archive/zip`, `archive/tar`** | overlay/shim | exports, backups, `xlsx` (zip+xml), bundle packaging. `System.IO.Compression`. |
| **`encoding/gob`, `encoding/csv`, `encoding/xml`, `encoding/base32`** | overlay | gob = Go-native cache/RPC serialization; csv/xml = business import/export. |
| **`net/smtp`** | shim | transactional email (business/SaaS) → `System.Net.Mail` or raw sockets. |
| **`text/scanner`** | overlay | hand-written parsers, some template/config libs. |

---

## 2. HTTP — correct the "client deferred" decision

The plan defers the HTTP **client**. That's wrong for SaaS and most business
apps: they *call* external APIs (payment, auth, webhooks, third-party SaaS) far
more than they serve. Promote to **P1**:

- **`net/http` client**: `Client`, `Get/Post/Head/Do`, `Transport`, `Request`
  building, response bodies, redirects, timeouts, `http.RoundTripper` → shim to
  `System.Net.Http.HttpClient` (keep a pooled static client).
- **`net/http/cookiejar`** + cookie handling.
- **`crypto/tls` (client side)**: outbound HTTPS via .NET TLS. Server-side TLS can
  ride Kestrel.
- **`net/http/httptest`**: `httptest.NewServer` / `NewRecorder` — needed to run the
  real `net/http` and Echo test suites under `goclr test`.
- **`compress/gzip`** in transport (Accept-Encoding) — ties to §34.

Keep deferred: HTTP/2 server internals, hijacker exactness, pprof, autocert.

---

## 3. `net` — promote for games + infra clients

The plan marks `net` "medium". For **games** (multiplayer TCP/UDP, dedicated
servers) and for infra clients (redis, postgres-over-wire, kafka, nats) it's
foundational:

- **TCP**: `Listen`/`Dial`/`Conn` → `System.Net.Sockets.TcpListener/TcpClient`.
- **UDP**: `ListenPacket`/`DialUDP`/`PacketConn` → `UdpClient`/`Socket` (games).
- **`net.IP`, `SplitHostPort/JoinHostPort`, `LookupHost/LookupIP`** → `Dns`.
- **`net.Conn` deadlines** (SetReadDeadline) → socket timeouts.

Without `net`, "SaaS that talks to Postgres/Redis over the wire" and "game server"
are both blocked, regardless of how good `net/http` is.

---

## 4. crypto — expand for auth (SaaS) and integrity (all)

The plan's M2.5 crypto (sha*, hmac, md5, subtle, rand) is right but incomplete for
real auth:

- **`golang.org/x/crypto/bcrypt`** (and `argon2`, `scrypt`) — **password
  hashing**, mandatory for any SaaS with accounts. Either compile-direct the
  pure-Go impl or shim to .NET KDFs (semantics must match the stored-hash format).
- **`crypto/ed25519`, `crypto/ecdsa`, `crypto/rsa`** — JWT signing, API keys,
  webhooks signature verification (Stripe/GitHub style). P2.
- **`crypto/aes` + `crypto/cipher`** — token/session encryption. P2.
- **`crypto/x509`, `crypto/tls` (server)** — P3 (mostly via Kestrel for serving).

Auth ecosystem track (third-party, all pure Go once crypto lands):
`golang-jwt/jwt`, `golang.org/x/oauth2`, `gorilla/sessions`, `markbates/goth`.

---

## 5. `reflect` is the keystone — call it out explicitly

The plan lists `reflect` as one package among many. It is **the** highest-leverage
runtime piece: `fmt %v`, `encoding/json`, `encoding/xml`, `gob`, `text/template`,
`GORM`, `validator`, `testify`, and `goja` all bottom out in it. Treat it as its
own mini-milestone with the compiler emitting the metadata it needs:

- compiler emits **type descriptors + method tables** for every type that can be
  reflected (kinds, fields, tags, methods, element/key types);
- `Value`: settable values, `Field/Index/MapIndex`, `Call`, `Make{Slice,Map,Chan}`,
  `New`, `Set*`, `Interface()`, `DeepEqual`;
- **struct tag parsing** (`json:"name,omitempty"`), embedded-field promotion;
- this is where the non-generic boxed representation (GoSlice/GoMap/GoPtr/GoString)
  pays off — reflection over boxed values is uniform.

Order `reflect` *before* json/fmt-%v/template; they are thin layers on top.

---

## 6. Third-party ecosystem tracks (per profile)

Real apps are 80% third-party. Most are pure Go and compile-direct once the
core + reflect land. Track them explicitly so "it compiled stdlib" doesn't get
mistaken for "the app compiles".

### SaaS
- **testify** (assert/require/mock/suite) — reflect; gates `goclr test` on real repos.
- **GORM** (`gorm.io/gorm`) + a driver — reflect-heavy ORM; the dominant data layer.
  Driver choice: pure-Go **`pgx`** (uses `net` + crypto) vs a managed
  `goclr/postgres` over Npgsql. Decide per §29 strategy.
- **`sqlx`** (lighter than GORM).
- **redis** (`redis/go-redis`) — net-based cache/queue. Very common.
- **validation** (`go-playground/validator`) — reflect + struct tags.
- **config** (`spf13/viper`, `joho/godotenv`, `kelseyhightower/envconfig`).
- **router/web** beyond Echo: `gin`, `chi`, `gorilla/mux` — all on `net/http`.
- **websocket** (`gorilla/websocket`, `coder/websocket`) — needs `net/http`
  upgrade/hijack; realtime SaaS + games.
- **observability**: `prometheus/client_golang`, OpenTelemetry, `rs/zerolog` /
  `uber/zap` (or stdlib `slog`).
- **gRPC + protobuf** (`google.golang.org/grpc`, `protobuf`) — microservices. P3
  (HTTP/2 + codegen).
- **uuid** (`google/uuid`), **cron** (`robfig/cron`), **rate limit**
  (`x/time/rate`), **errgroup** (`x/sync/errgroup`).

### Business
- **decimal/money** (`shopspring/decimal`) — math/big; never use float for money.
- **Excel** (`xuri/excelize`) — archive/zip + encoding/xml; reporting.
- **PDF** (`jung-kurt/gofpdf`, `signintech/gopdf`) — pure Go.
- **CSV/XML** (stdlib), **i18n** (`golang.org/x/text`), **timezones**
  (`time/tzdata` embed — depends on §0.2).
- **templating** (`html/template`, have), **email** (`net/smtp` §1).

### Games
- **`math/rand` (seeded), `math/bits`, `math`** — core loop, procedural gen.
- **`net` TCP/UDP** — multiplayer, dedicated servers.
- **serialization**: `gob`, `protobuf`, `vmihailenco/msgpack`, `flatbuffers`.
- **pure-Go logic libs** (ECS like `yohamta/donburi`, pathfinding, noise, physics
  `jakecoffman/cp`) — compile-direct.
- **graphics/input/audio** → see §7 (the fork).

---

## 7. Games track — the rendering fork (architectural decision)

Games split cleanly into two worlds; the plan needs to pick:

- **Headless / server-authoritative / logic-only** (board/card games, MUD/text,
  game *servers*, simulations, bots): work today once core + `net` + `math/rand`
  land. **Make this the M2.5 games target.**
- **Client-side graphical** (Ebiten, raylib-go, pixel, gioui, fyne): need a
  rendering + input + audio backend. The Go libs assume cgo/GL/GLFW → **blocked**
  as-is on CLR. Two real options:
  - **(A) Scope out for M2.5** — only headless games. Recommended for the MVP.
  - **(B) Ebiten-compatible overlay on .NET** — implement Ebiten's small public
    surface (`ebiten.Game`, `Run`, `Image.DrawImage`, input, audio) on
    **Silk.NET** (GL/windowing) or **MonoGame**; audio via NAudio. This is a
    *separate milestone* (M6+), large but bounded because Ebiten's API is small.
    Alternative: target **WASM** (Ebiten supports it) — but that's a non-CLR path.

Decision required before promising "games". Recommend: **(A) for M2.5, (B) as an
optional M6 "Ebiten/Silk.NET" milestone**, and say so explicitly so nobody assumes
graphical games run on day one.

---

## 8. Revised priority matrix (merging the plan's P0 with these additions)

Tagged by target class: **[C]ore-all · [S]aaS · [B]usiness · [G]ames**.

```
P0  (unblocks everything; do first)
  foundations: overlay resolver, //go:embed, build-tag/GOARCH decision,
               float-ftoa + time-layout parity, reflect            [C]
  errors, strconv, strings, bytes, unicode/utf8/utf16/unicode      [C]
  io, io/fs, bufio, fmt, math, math/bits, math/rand                [C][G]
  sync, sync/atomic, time, context, runtime(+debug)                [C]
  reflect, encoding/json, regexp(+regexp2 patch), hash/fnv+crc32   [C][S]
  goja typedarrays managed patch                                    (goja)

P1  (real services)
  os(+signal+exec), path, filepath, log/slog, flag                 [C]
  net (TCP/UDP/dns)                                                 [S][G]
  net/url, mime, mime/multipart                                    [S]
  net/http server (Kestrel) + client + httptest + cookiejar        [S]
  crypto/sha*+hmac+rand+subtle, x/crypto/bcrypt                    [S]
  container/heap+list, x/sync/errgroup, uuid                       [C][S]

P2  (product depth)
  database/sql + driver + goclr/sqlite|postgres, sqlx/GORM         [S][B]
  encoding/csv+xml+gob+base64+hex+binary, archive/zip+tar          [B]
  decimal(math/big), excelize, gofpdf, net/smtp                    [B]
  validator, viper/godotenv, websocket, crypto/aes+ed25519, JWT    [S]
  compress/gzip+flate+zlib                                         [S][B]

P3  (scale / ecosystem)
  gRPC + protobuf, redis, prometheus/otel, html/template           [S]
  crypto/rsa+ecdsa+x509+tls(server), oauth2/sessions               [S]
  Ebiten/Silk.NET graphical games overlay                          [G]
  testing full + testify parity, goclr test on real repos          [C]

P4  (perf / packaging)
  AOT, trimming, PDB/debug symbols, slice/map generic specialization,
  GC tuning, struct layout/value-type specialization              [C]
```

---

## 9. Per-profile "definition of done" (extend the closing criteria)

The plan's 10 closing programs are good but skew goja/Echo/SQL. Add a smoke test
per target class so "M2.5 done" means "real apps of each kind run":

**SaaS smoke** (must run, output ≡ `go run`/`go test`):
```go
// http client + json + bcrypt + jwt + context + errgroup
resp, _ := http.Get("https://api.example.com/health")     // net/http client
var out map[string]any; json.NewDecoder(resp.Body).Decode(&out)
hash, _ := bcrypt.GenerateFromPassword([]byte("pw"), 10)   // x/crypto
tok, _ := jwt.NewWithClaims(...).SignedString(key)          // golang-jwt
g, ctx := errgroup.WithContext(context.Background())        // x/sync
```

**Business smoke**:
```go
// decimal money + csv + time/tz + excelize roundtrip
d := decimal.NewFromFloat(19.99).Mul(decimal.NewFromInt(3)) // shopspring
w := csv.NewWriter(os.Stdout); w.Write([]string{d.String()})
t, _ := time.ParseInLocation("2006-01-02", "2026-06-19", loc)
f := excelize.NewFile(); f.SetCellValue("Sheet1", "A1", d.String())
```

**Games smoke** (headless, the M2.5 target):
```go
// seeded rng determinism + tcp server + msgpack + heap
r := rand.New(rand.NewSource(42)); _ = r.Intn(100)          // must match Go's seq
ln, _ := net.Listen("tcp", ":0")                            // game server
b, _ := msgpack.Marshal(GameState{Tick: 1})                 // serialization
heap.Init(&pq)                                              // scheduler/AI
```

**Tooling smoke**:
```go
// testify under goclr test
require.Equal(t, 42, compute())
mockObj.On("Get", 1).Return("x")
```

If those four run byte-for-byte under `goclr` (or pass under `goclr test`), M2.5
covers the real surface for business + games(headless) + SaaS — not just the
echo+goja demo.

---

## 10. Explicitly-blocked / out-of-scope (extend the plan's list)

Add to the plan's blocked set, with the reason, so they fail loudly not silently:

- `plugin`, `runtime/cgo`, `runtime/pprof`, `runtime/trace`, `net/http/pprof` —
  runtime introspection / dynamic loading.
- full `syscall`, `os/signal` beyond INT/TERM, `golang.org/x/sys/*` asm paths.
- `debug/*` (dwarf/elf/macho/pe), `cmd/*`, `internal/*`.
- cgo-bound libs: `mattn/go-sqlite3` (use managed driver), graphical Ebiten/raylib
  without the §7(B) backend, anything binding GL/GLFW/SDL/PortAudio directly.
- general `unsafe`: only approved patterns; replace with `//go:build goclr` files.

The litmus test stays: **pure Go + no syscall/asm/unsafe ⇒ compile-direct;
runtime/OS-bound ⇒ overlay or shim; impossible/dangerous ⇒ blocked, loudly.**
