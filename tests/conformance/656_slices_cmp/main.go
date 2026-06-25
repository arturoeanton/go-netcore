package main

import (
	"cmp"
	"fmt"
	"slices"
)

// slices/cmp overlay. Element types erase to boxed values; the backend now unboxes a generic
// shim's result, so functions returning a type parameter (Max/Min, Clone/Compact/Concat,
// cmp.Or) work alongside the concrete-return ones (Sort, Contains, Index, Equal, BinarySearch).
func main() {
	s := []int{3, 1, 2, 1}
	slices.Sort(s)
	fmt.Println(s)
	fmt.Println(slices.Contains(s, 2), slices.Index(s, 2), slices.IsSorted(s))
	fmt.Println(slices.BinarySearch(s, 2))
	fmt.Println(slices.Equal([]int{1, 2}, []int{1, 2}), slices.Equal([]int{1}, []int{2}))

	r := []int{1, 2, 3, 4, 5}
	slices.Reverse(r)
	fmt.Println(r)
	fmt.Println(slices.ContainsFunc(s, func(x int) bool { return x > 2 }))
	fmt.Println(slices.IndexFunc(s, func(x int) bool { return x == 2 }))

	// generic-return functions (unboxed from the shim's object result)
	fmt.Println(slices.Max([]int{1, 3, 2}), slices.Min([]int{1, 3, 2}))
	fmt.Println(slices.Max([]float64{1.5, 3.2, 0.1}), slices.Min([]string{"a", "c", "b"}))
	a := slices.Clone([]int{1, 2, 3})
	a[0] = 99
	fmt.Println(a, len(a))
	fmt.Println(slices.Compact([]int{1, 1, 2, 3, 3}))
	fmt.Println(slices.Concat([]int{1, 2}, []int{3}, []int{4, 5}))
	fmt.Println(slices.Max(slices.Clone([]int{4, 7, 2})) + 1)

	ppl := []string{"bb", "a", "ccc"}
	slices.SortFunc(ppl, func(a, b string) int { return cmp.Compare(len(a), len(b)) })
	fmt.Println(ppl)
	fmt.Println(slices.MaxFunc(ppl, func(a, b string) int { return cmp.Compare(len(a), len(b)) }))

	ss := []string{"banana", "apple", "cherry"}
	slices.Sort(ss)
	fmt.Println(ss, slices.IsSortedFunc(ss, func(a, b string) int { return cmp.Compare(a, b) }))
	idx, found := slices.BinarySearchFunc(ss, "banana", func(a, b string) int { return cmp.Compare(a, b) })
	fmt.Println(idx, found)

	fl := []float64{3.2, 1.1, 2.5}
	slices.Sort(fl)
	fmt.Println(fl)

	fmt.Println(cmp.Compare(1, 2), cmp.Less("a", "b"), cmp.Or(0, 0, 5), cmp.Or("", "x", "y"))
}
