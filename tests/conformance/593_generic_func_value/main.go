package main

import (
	"cmp"
	"fmt"
	"slices"
)

// A generic function instantiation used as a *value* (not immediately called) —
// cmp.Compare[int], Identity[string], Pair[K,V] — monomorphizes to a function value.
func Identity[T any](x T) T { return x }
func Pair[K comparable, V any](k K, v V) string { return fmt.Sprintf("%v=%v", k, v) }

func main() {
	// bind and call
	f := cmp.Compare[int]
	fmt.Println(f(3, 5), f(5, 3), f(4, 4))

	g := Identity[string]
	fmt.Println(g("hi"))

	// multi type parameter (IndexListExpr)
	p := Pair[string, int]
	fmt.Println(p("a", 1))

	// passed straight as an argument to higher-order stdlib funcs
	xs := []int{3, 1, 4, 1, 5, 9, 2, 6}
	slices.SortFunc(xs, cmp.Compare[int])
	fmt.Println(xs)
	fmt.Println(slices.MaxFunc(xs, cmp.Compare[int]), slices.MinFunc(xs, cmp.Compare[int]))
	found, idx := slices.BinarySearchFunc(xs, 5, cmp.Compare[int])
	fmt.Println(found, idx)

	// stored in a slice of funcs and invoked
	cmps := []func(int, int) int{cmp.Compare[int], func(a, b int) int { return b - a }}
	fmt.Println(cmps[0](2, 8), cmps[1](2, 8))

	// reused value
	c := cmp.Compare[string]
	fmt.Println(c("b", "a"), c("a", "b"), c("a", "a"))

	// directly called instantiation still works
	fmt.Println(cmp.Compare[int](1, 2), Identity[int](42))
}
