package main

import (
	"fmt"
	"maps"
	"slices"
)

// iter.Seq support: slices.Values/All/Backward and maps.Keys/Values/All return iterators
// (range-over-func), and slices.Collect/Sorted/SortedFunc drain them. Map iteration order is
// unspecified, so map iterators are used via Sorted or an order-independent sum.
func main() {
	m := map[string]int{"c": 3, "a": 1, "b": 2}
	fmt.Println(slices.Sorted(maps.Keys(m)))
	fmt.Println(slices.Sorted(maps.Values(m)))

	fmt.Println(slices.Collect(slices.Values([]int{5, 6, 7})))
	fmt.Println(slices.Sorted(slices.Values([]int{3, 1, 2})))
	fmt.Println(slices.SortedFunc(slices.Values([]string{"bb", "a", "ccc"}), func(a, b string) int { return len(a) - len(b) }))

	for i, v := range slices.All([]string{"x", "y", "z"}) {
		fmt.Print(i, ":", v, " ")
	}
	fmt.Println()
	for i, v := range slices.Backward([]int{10, 20, 30}) {
		fmt.Print(i, ":", v, " ")
	}
	fmt.Println()

	// order-independent aggregate over map iterators
	ksum, vsum := 0, 0
	for k := range maps.Keys(m) {
		ksum += len(k)
	}
	for _, v := range maps.All(m) {
		vsum += v
	}
	fmt.Println(ksum, vsum)

	// early break on an iterator
	cnt := 0
	for range slices.Values([]int{1, 2, 3, 4, 5}) {
		cnt++
		if cnt == 3 {
			break
		}
	}
	fmt.Println(cnt)
}
