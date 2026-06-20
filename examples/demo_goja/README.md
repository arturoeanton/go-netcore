# demo_goja — a JavaScript engine running as a .NET assembly

This embeds [goja](https://github.com/dop251/goja), a JavaScript interpreter
written in pure Go, and evaluates JavaScript from it. Compiled with `goclr`, the
entire program — interpreter included — becomes an ECMA-335 .NET assembly that runs
on `dotnet`. No C# host, no JS runtime: a Go JS engine executing as managed CLR
code.

```sh
# build to a .NET dll and run it
goclr run .
```

Expected output (identical to `go run .`):

```
1 + 2 * 3                        => 7
(7 - 1) / 2                      => 3
2 * (3 + 4) - 5                  => 9
"Hello, " + "goclr" + "!"        => Hello, goclr!
"a" + "b" + "c" + "d"            => abcd
(function (x) { return x * x + 1; })(9) => 82
```

## Status

goja **compiles** (a ~15 MB IL assembly), **loads and JITs cleanly**, runs its full
package init, parses and compiles JavaScript, and **evaluates** the subset above
(arithmetic, string concatenation, function calls). Richer features — string/array
prototype methods, `for`-loops inside functions, `Math.*`, JSON — reach further
runtime gaps tracked in `../../GAPS.md`. Getting a dependency this heavy to run at
all exercises a very large surface of the compiler, so most lighter libraries work.
