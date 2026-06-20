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

	// Expose a Go function to JavaScript.
	vm.Set("greet", func(name string) string {
		return "Hello, " + name + "!"
	})

	// A non-trivial script: closures, array methods, objects, and a call back
	// into Go.
	script := `
		function fib(n) {
			var a = 0, b = 1;
			for (var i = 0; i < n; i++) { var t = a + b; a = b; b = t; }
			return a;
		}
		var nums = [];
		for (var i = 1; i <= 10; i++) nums.push(fib(i));
		var total = nums.reduce(function (s, x) { return s + x; }, 0);

		var result = {
			message: greet("goclr"),
			fibs: nums,
			sum: total,
			upper: "saas".toUpperCase()
		};
		result;
	`

	v, err := vm.RunString(script)
	if err != nil {
		fmt.Println("js error:", err)
		return
	}

	obj := v.ToObject(vm)
	fmt.Println("message:", obj.Get("message"))
	fmt.Println("fibs:   ", obj.Get("fibs"))
	fmt.Println("sum:    ", obj.Get("sum"))
	fmt.Println("upper:  ", obj.Get("upper"))
}
