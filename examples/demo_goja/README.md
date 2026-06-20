# demo_goja — a JavaScript engine running as a .NET assembly

This embeds [goja](https://github.com/dop251/goja), a JavaScript interpreter
written in pure Go, and runs a script from it. Compiled with `goclr`, the entire
program — interpreter included — becomes an ECMA-335 .NET assembly that runs on
`dotnet`. No C# host, no JS runtime: a Go JS engine executing as managed CLR code.

```sh
# build to a .NET dll and run it
goclr run .
```

Status: the program is correct Go (`go run .` works today). Running it under
`goclr` is **blocked on the typed-box keystone** (`docs/DESIGN-typed-box.md`):
goja pulls in `golang.org/x/text/collate`, whose `sort.StringSlice` dispatch
requires per-value type identity. This example is the north-star target that
drives that work.
