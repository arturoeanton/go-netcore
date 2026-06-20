// demo_goja: embed a JavaScript engine (goja) inside a .NET assembly produced by
// goclr. The whole program — including the pure-Go JS interpreter — is compiled to
// ECMA-335 IL and runs on the CLR with `dotnet`. No C#, no JS host: a Go JS engine
// running as managed .NET code.
package main

import (
	"fmt"

	"github.com/dop251/goja"
)

func main() {
	vm := goja.New()

	scripts := []string{
		`1 + 2 * 3`,
		`"saas platform".toUpperCase()`,
		`Math.max(3, 9, 4) + Math.floor(2.7)`,
		`var s = 0; for (var i = 1; i <= 100; i++) s += i; s`,
		`[1, 2, 3, 4, 5].filter(function (x) { return x % 2 }).map(function (x) { return x * x })`,
		`[5, 3, 8, 1].sort(function (a, b) { return a - b }).join(",")`,
		`"hello world".split(" ").map(function (w) { return w.toUpperCase() }).join("_")`,
		`Object.keys({ name: "goclr", kind: "compiler" }).join(", ")`,
		`JSON.stringify({ ok: true, items: [1, 2, 3], nested: { x: 42 } })`,
		`JSON.parse('{"total": 5050, "tags": ["a", "b"]}').tags[1]`,
	}

	for _, src := range scripts {
		v, err := vm.RunString(src)
		if err != nil {
			fmt.Printf("%-46s ERROR: %v\n", clip(src), err)
			continue
		}
		fmt.Printf("%-46s => %v\n", clip(src), v.Export())
	}
}

func clip(s string) string {
	if len(s) > 44 {
		return s[:41] + "..."
	}
	return s
}
