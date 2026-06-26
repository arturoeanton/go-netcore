package main

import (
	"cmp"
	"fmt"
	"slices"
)

// An explicit generic instantiation of a shimmed function used as a *value* —
// cmp.Compare[int], cmp.Less[string] — must lower to a func value, not be parsed
// as array/map indexing (which previously raised GCLR0301).
func main() {
	s := []int{3, 1, 2}
	slices.SortFunc(s, cmp.Compare[int])
	fmt.Println(s)

	ss := []string{"banana", "apple", "cherry"}
	slices.SortFunc(ss, cmp.Compare[string])
	fmt.Println(ss)

	fmt.Println(slices.MinFunc([]int{5, 2, 8}, cmp.Compare[int]))
	fmt.Println(slices.MaxFunc([]float64{1.5, 2.5, 0.5}, cmp.Compare[float64]))
	fmt.Println(slices.IsSortedFunc([]int{1, 2, 3}, cmp.Compare[int]))

	idx, found := slices.BinarySearchFunc([]int{1, 3, 5, 7}, 5, cmp.Compare[int])
	fmt.Println(idx, found)

	// cmp.Less / cmp.Compare stored in a variable, then called.
	less := cmp.Less[int]
	fmt.Println(less(1, 2), less(2, 1), less(1, 1))
	cf := cmp.Compare[string]
	fmt.Println(cf("a", "b"), cf("b", "b"), cf("c", "a"))

	// Passing through a higher-order helper.
	apply := func(f func(int, int) int, a, b int) int { return f(a, b) }
	fmt.Println(apply(cmp.Compare[int], 3, 9))
}
