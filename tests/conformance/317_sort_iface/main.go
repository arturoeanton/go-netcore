package main

import (
	"fmt"
	"sort"
)

// byLen is a single named-slice implementer of sort.Interface (unambiguous).
type byLen []string

func (b byLen) Len() int           { return len(b) }
func (b byLen) Less(i, j int) bool { return len(b[i]) < len(b[j]) }
func (b byLen) Swap(i, j int)      { b[i], b[j] = b[j], b[i] }

// records is sorted via a pointer-to-struct implementer (goja's pattern).
type records struct{ vals []int }

func (r *records) Len() int           { return len(r.vals) }
func (r *records) Less(i, j int) bool { return r.vals[i] < r.vals[j] }
func (r *records) Swap(i, j int)      { r.vals[i], r.vals[j] = r.vals[j], r.vals[i] }

func main() {
	s := []string{"ccc", "a", "bb", "dddd", "e"}
	sort.Sort(byLen(s))
	fmt.Println(s)

	r := &records{vals: []int{9, 3, 7, 1, 5}}
	sort.Stable(r)
	fmt.Println(r.vals)
	fmt.Println(sort.IsSorted(r))

	// Shimmed helpers still work alongside source-compiled Sort/Stable.
	n := []int{5, 2, 8, 1, 9}
	sort.Ints(n)
	fmt.Println(n, sort.SearchInts(n, 8))
	fmt.Println(sort.Search(20, func(i int) bool { return i*i >= 100 }))
}
