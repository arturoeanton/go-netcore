package main

import (
	"fmt"
	"strconv"
)

// A package-level function referenced as a value (not called) — here strconv.Itoa
// stored in a map and a slice, then invoked through the value. Exercises the
// qualified-func-value lowering (pkg.Func as an expression).
func apply(f func(int) string, n int) string { return f(n) }

func main() {
	conv := map[string]func(int) string{"itoa": strconv.Itoa}
	fmt.Println(conv["itoa"](42))

	fns := []func(int) string{strconv.Itoa}
	fmt.Println(fns[0](7))

	fmt.Println(apply(strconv.Itoa, 100))
}
