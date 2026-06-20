// demo_goja: embed a JavaScript engine (goja) inside a .NET assembly produced by
// goclr. The whole program — including the pure-Go JS interpreter — is compiled to
// ECMA-335 IL and runs on the CLR with `dotnet`. No C#, no JS host: a Go JS engine
// running as managed .NET code.
//
// NOTE: goja support is in progress. This demo uses the subset that already runs
// end-to-end on the CLR (arithmetic, string concatenation, a function call). Richer
// features (string/array methods, for-loops inside functions) are tracked in GAPS.md.
package main

import (
	"fmt"

	"github.com/dop251/goja"
)

func main() {
	vm := goja.New()

	scripts := []string{
		`1 + 2 * 3`,
		`(7 - 1) / 2`,
		`2 * (3 + 4) - 5`,
		`"Hello, " + "goclr" + "!"`,
		`"a" + "b" + "c" + "d"`,
		`(function (x) { return x * x + 1; })(9)`,
	}

	for _, src := range scripts {
		v, err := vm.RunString(src)
		if err != nil {
			fmt.Printf("%-32s ok=false\n", src)
			continue
		}
		fmt.Printf("%-32s => %v\n", src, v.Export())
	}
}
