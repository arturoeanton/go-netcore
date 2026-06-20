package main

import (
	"cmp"
	"fmt"
	"slices"
	"sort"
)

func main() {
	// Go 1.21 max/min builtins over ints, strings, floats, and 3+ args.
	fmt.Println(max(3, 7), min(3, 7))
	fmt.Println(max(1, 5, 3, 9, 2), min(8, 2, 6, 1, 4))
	fmt.Println(max("apple", "banana"), min("x", "a", "m"))
	fmt.Printf("%.1f %.1f\n", max(1.5, 2.5), min(3.3, 1.1))

	// cmp
	fmt.Println(cmp.Less(3, 5), cmp.Compare(7, 2), cmp.Or(0, 0, 4))

	// slices
	a := []int{3, 1, 2}
	fmt.Println(slices.Equal(a, []int{3, 1, 2}), slices.Contains(a, 2), slices.Index(a, 1))
	slices.Sort(a)
	fmt.Println(a, slices.Max(a), slices.Min(a))
	s := []string{"banana", "apple", "fig"}
	slices.SortFunc(s, func(x, y string) int { return len(x) - len(y) })
	fmt.Println(s)

	// sort.StringSlice / IntSlice as Interface implementers (typed box dispatch)
	ss := sort.StringSlice{"pear", "kiwi", "fig"}
	ss.Sort()
	fmt.Println(ss, ss.Search("kiwi"))
	is := sort.IntSlice{9, 4, 7, 1}
	is.Sort()
	fmt.Println(is)
}
