package main

import (
	"fmt"
	"sort"
)

func main() {
	// Go 1.22: three-clause for, captured loop variable is per-iteration.
	var a []func() int
	for i := 0; i < 4; i++ {
		a = append(a, func() int { return i })
	}
	for _, f := range a {
		fmt.Print(f(), " ")
	}
	fmt.Println()

	// range over slice: captured value is per-iteration.
	var b []func() string
	for _, v := range []string{"x", "y", "z"} {
		b = append(b, func() string { return v })
	}
	for _, f := range b {
		fmt.Print(f(), " ")
	}
	fmt.Println()

	// range over int.
	var c []func() int
	for i := range 3 {
		c = append(c, func() int { return i * i })
	}
	for _, f := range c {
		fmt.Print(f(), " ")
	}
	fmt.Println()

	// captured map keys (sort the captured results for determinism).
	var d []func() string
	for k := range map[string]int{"a": 1, "b": 2, "c": 3} {
		d = append(d, func() string { return k })
	}
	got := make([]string, 0, len(d))
	for _, f := range d {
		got = append(got, f())
	}
	sort.Strings(got)
	fmt.Println(got)

	// body mutation of the loop var is observed by the closure and the loop.
	var e []func() int
	for i := 0; i < 3; i++ {
		i := i
		i *= 10
		e = append(e, func() int { return i })
	}
	for _, f := range e {
		fmt.Print(f(), " ")
	}
	fmt.Println()
}
