// demo_goja: embed a JavaScript engine (goja) inside a .NET assembly produced by
// goclr. The whole program — including the pure-Go JS interpreter — is compiled to
// ECMA-335 IL and runs on the CLR with `dotnet`. No C#, no JS host: a Go JS engine
// running as managed .NET code.
//
// goja support is in progress; this demo uses the (already large) subset that runs
// end-to-end on the CLR. Some advanced paths (array map/reduce callbacks,
// JSON.stringify) are still being completed — see ../../GAPS.md.
package main

import (
	"fmt"

	"github.com/dop251/goja"
)

func main() {
	vm := goja.New()

	scripts := []string{
		`1 + 2 * 3`,
		`"Hello, " + "goclr" + "!"`,
		`"saas platform".toUpperCase()`,
		`"abcdef".slice(1, 4)`,
		`Math.max(3, 9, 4) + Math.floor(2.7)`,
		`var o = { x: 10, y: 32 }; o.x + o.y`,
		`var s = 0; for (var i = 1; i <= 100; i++) s += i; s`,
		`var n = 6, f = 1; while (n > 1) { f *= n; n--; } f`,
		`(function (n) { var a = 0, b = 1; for (var i = 0; i < n; i++) { var t = a + b; a = b; b = t; } return a; })(15)`,
	}

	for _, src := range scripts {
		v, err := vm.RunString(src)
		if err != nil {
			fmt.Printf("%-40s ERROR\n", clip(src))
			continue
		}
		fmt.Printf("%-40s => %v\n", clip(src), v.Export())
	}
}

func clip(s string) string {
	if len(s) > 38 {
		return s[:35] + "..."
	}
	return s
}
