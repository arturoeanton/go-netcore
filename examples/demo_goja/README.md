# demo_goja — a JavaScript engine running as a .NET assembly

This embeds [goja](https://github.com/dop251/goja), a JavaScript interpreter
written in pure Go, and evaluates JavaScript from it. Compiled with `goclr`, the
entire program — interpreter included — becomes an ECMA-335 .NET assembly that runs
on `dotnet`. No C# host, no JS runtime: a Go JS engine executing as managed CLR
code.

```sh
# On a fresh checkout, recreate vendor/ first so the goja/regexp2 overlays apply
# (vendor/ is gitignored — see ../../goclr.overlays/README.md):
go mod vendor

goclr run .          # build to a .NET dll and run it
# or, without installing the binary:
go run ../../cmd/goclr/main.go run .
```

> Without `vendor/`, the build fails with `GCLR0201: unsupported unsafe operation`
> because goja's `regexp2`/typedarray `unsafe.Pointer` code is read straight from
> the module cache, where the goclr overlays cannot be applied.

Output (identical to `go run .`):

```
1 + 2 * 3                                => 7
"Hello, " + "goclr" + "!"                => Hello, goclr!
"saas platform".toUpperCase()            => SAAS PLATFORM
"abcdef".slice(1, 4)                     => bcd
Math.max(3, 9, 4) + Math.floor(2.7)      => 11
var o = { x: 10, y: 32 }; o.x + o.y      => 42
var s = 0; for (var i = 1; i <= 100...   => 5050
var n = 6, f = 1; while (n > 1) { f...   => 720
(function (n) { var a = 0, b = 1; f...   => 610
```

## Status

goja **compiles** (a ~15 MB IL assembly), **loads and JITs cleanly**, runs full
package init, and **evaluates a large JavaScript subset**: arithmetic, strings and
string methods (`toUpperCase`, `slice`, `split`, `join`), `Math`, objects and
property access (`Object.keys`), function calls/closures, `for`/`while` loops,
array callbacks (`filter`/`map`/`sort`), and `JSON.stringify`/`JSON.parse` — all
exercised by [`main.go`](main.go) and byte-exact against upstream goja. Getting a
dependency this heavy to run exercises a very large surface of the compiler and
runtime, so most lighter libraries work. See [`../../docs/GAPS.md`](../../docs/GAPS.md)
for remaining edges.
