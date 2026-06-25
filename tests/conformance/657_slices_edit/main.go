package main

import (
	"cmp"
	"fmt"
	"slices"
)

// slices editing/compare functions: Insert/Delete/Replace (variadic), DeleteFunc, Repeat, and
// Compare/CompareFunc (lexicographic, shorter prefix is smaller). All return []E or int,
// exercising the generic-return unbox path.
func main() {
	fmt.Println(slices.Insert([]int{1, 2, 5}, 2, 3, 4))
	fmt.Println(slices.Insert([]int{1, 2, 3}, 0, 0))
	fmt.Println(slices.Insert([]string{"a", "c"}, 1, "b"))
	fmt.Println(slices.Delete([]int{1, 2, 3, 4, 5}, 1, 3))
	fmt.Println(slices.Replace([]int{1, 2, 3, 4}, 1, 3, 9, 9, 9))
	fmt.Println(slices.DeleteFunc([]int{1, 2, 3, 4, 5, 6}, func(x int) bool { return x%2 == 0 }))
	fmt.Println(slices.Repeat([]int{1, 2}, 3))
	fmt.Println(slices.Repeat([]string{"x"}, 0))

	fmt.Println(slices.Compare([]int{1, 2, 3}, []int{1, 2, 4}))
	fmt.Println(slices.Compare([]int{1, 2}, []int{1, 2, 3}))
	fmt.Println(slices.Compare([]int{1, 2}, []int{1, 2}))
	fmt.Println(slices.Compare([]string{"a"}, []string{"b"}))
	fmt.Println(slices.CompareFunc([]int{3, 1}, []int{1, 3}, func(a, b int) int { return cmp.Compare(a, b) }))

	x := slices.Insert([]int{5, 1}, 1, 3)
	slices.Sort(x)
	fmt.Println(x)
}
