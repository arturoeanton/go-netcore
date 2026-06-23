package main

import (
	"fmt"
	"sort"
)

// A user type implementing sort.Interface, plus sort.Reverse and the package's
// own IntSlice/StringSlice adapters.
type byLen []string

func (s byLen) Len() int           { return len(s) }
func (s byLen) Less(i, j int) bool { return len(s[i]) < len(s[j]) }
func (s byLen) Swap(i, j int)      { s[i], s[j] = s[j], s[i] }

func main() {
	s := byLen{"ccc", "a", "bb"}
	sort.Sort(s)
	fmt.Println(s)
	sort.Sort(sort.Reverse(s))
	fmt.Println(s)
	fmt.Println(sort.IsSorted(byLen{"a", "bb", "ccc"}))

	i := []int{3, 1, 2}
	sort.Sort(sort.IntSlice(i))
	fmt.Println(i)
	sort.Sort(sort.Reverse(sort.IntSlice(i)))
	fmt.Println(i)

	st := []string{"banana", "apple", "cherry"}
	sort.Sort(sort.StringSlice(st))
	fmt.Println(st)
	fmt.Println(sort.IntSlice([]int{1, 2, 3, 5}).Search(3))

	// sort.Stable keeps equal elements in original order.
	type kv struct {
		K string
		V int
	}
	d := []kv{{"a", 2}, {"b", 1}, {"c", 2}, {"d", 1}}
	sort.SliceStable(d, func(i, j int) bool { return d[i].V < d[j].V })
	fmt.Println(d)
}
