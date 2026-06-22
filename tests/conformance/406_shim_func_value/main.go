package main

import (
	"fmt"
	"strings"
	"unicode"
)

func apply(f func(string) string, s string) string { return f(s) }

func main() {
	// A shimmed stdlib function taken as a value.
	up := strings.ToUpper
	fmt.Println(up("hello"))

	// Passed as an argument.
	fmt.Println(apply(strings.ToLower, "WORLD"))

	// In a slice of func values.
	fns := []func(string) string{strings.ToUpper, strings.TrimSpace}
	fmt.Println(fns[0]("ab"), "|", fns[1]("  x  "))

	// A predeclared-from-source function (unicode) as a callback to a shim.
	fmt.Printf("%q\n", strings.TrimFunc("  Go!  ", unicode.IsSpace))
	fmt.Println(strings.Map(unicode.ToUpper, "abc"))

	// A VARIADIC shimmed function as a value: the trailing args must be packed.
	sp := fmt.Sprintf
	fmt.Println(sp("%d-%s-%v", 7, "x", true))
	fmt.Println(sp("no-args"))

	// A method value of a shim type.
	var b strings.Builder
	w := b.WriteString
	w("a")
	w("bc")
	fmt.Println(b.String())
}
