package main

import (
	"fmt"
	"index/suffixarray"
	"sort"
)

func main() {
	idx := suffixarray.New([]byte("mississippi"))
	// Lookup returns offsets in unspecified order; sort to compare the set.
	for _, q := range []string{"i", "issi", "ss", "p", "z", "mississippi", ""} {
		o := idx.Lookup([]byte(q), -1)
		sort.Ints(o)
		fmt.Printf("%q -> %v\n", q, o)
	}
	// n == 0 yields nil; n > 0 caps the count.
	fmt.Println("n0:", idx.Lookup([]byte("i"), 0))
	got := idx.Lookup([]byte("s"), 2)
	fmt.Println("limit2 count:", len(got))

	// Bytes() returns the indexed data.
	fmt.Printf("data=%s\n", idx.Bytes())
}
