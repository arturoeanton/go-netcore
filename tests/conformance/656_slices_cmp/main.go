package main

import (
	"cmp"
	"fmt"
	"slices"
)

// slices/cmp overlay (concrete-return subset): Sort/SortFunc, Contains(Func), Index(Func),
// Equal(Func), Reverse, IsSorted(Func), BinarySearch(Func), and cmp.Compare/Less. Element
// types erase to boxed values; ordering uses Go's byte-wise string compare and numeric order.
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

	ppl := []string{"bb", "a", "ccc"}
	slices.SortFunc(ppl, func(a, b string) int { return cmp.Compare(len(a), len(b)) })
	fmt.Println(ppl)

	ss := []string{"banana", "apple", "cherry"}
	slices.Sort(ss)
	fmt.Println(ss, slices.IsSortedFunc(ss, func(a, b string) int { return cmp.Compare(a, b) }))
	idx, found := slices.BinarySearchFunc(ss, "banana", func(a, b string) int { return cmp.Compare(a, b) })
	fmt.Println(idx, found)

	fl := []float64{3.2, 1.1, 2.5}
	slices.Sort(fl)
	fmt.Println(fl)

	fmt.Println(cmp.Compare(1, 2), cmp.Less("a", "b"), cmp.Compare(3.5, 3.5), cmp.Less(2.0, 1.0))
}
