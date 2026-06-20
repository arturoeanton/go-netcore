package main

import (
	"fmt"
	"sort"
)

// Two named-slice types implementing sort.Interface — distinct representations
// (both collapse to one backing slice) must dispatch to their own methods.
type ByLen []string

func (b ByLen) Len() int           { return len(b) }
func (b ByLen) Less(i, j int) bool { return len(b[i]) < len(b[j]) }
func (b ByLen) Swap(i, j int)      { b[i], b[j] = b[j], b[i] }

type Desc []int

func (d Desc) Len() int           { return len(d) }
func (d Desc) Less(i, j int) bool { return d[i] > d[j] }
func (d Desc) Swap(i, j int)      { d[i], d[j] = d[j], d[i] }

func main() {
	words := ByLen{"ccc", "a", "bb", "dddd"}
	sort.Sort(words)
	fmt.Println(words)

	nums := Desc{3, 1, 4, 1, 5, 9, 2, 6}
	sort.Sort(nums)
	fmt.Println(nums)

	// interleave dispatch to be sure each picks its own Less/Swap
	a := ByLen{"xyz", "q", "mn"}
	b := Desc{2, 8, 1}
	sort.Sort(a)
	sort.Sort(b)
	fmt.Println(a, b)
	fmt.Println(sort.IsSorted(a), sort.IsSorted(b))
}
