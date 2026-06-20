# demo_goja — a JavaScript engine running as a .NET assembly

This embeds [goja](https://github.com/dop251/goja), a JavaScript interpreter
written in pure Go, and evaluates JavaScript from it. Compiled with `goclr`, the
entire program — interpreter included — becomes an ECMA-335 .NET assembly that runs
on `dotnet`. No C# host, no JS runtime: a Go JS engine executing as managed CLR
code.

```sh
goclr run .          # build to a .NET dll and run it
```

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
string methods (`toUpperCase`, `slice`), `Math`, objects and property access,
function calls/closures, and `for`/`while` loops. Some advanced paths — array
callbacks (`map`/`reduce`), `JSON.stringify` — are still being completed; see
`../../GAPS.md`. Getting a dependency this heavy to run exercises a very large
surface of the compiler and runtime, so most lighter libraries work.
