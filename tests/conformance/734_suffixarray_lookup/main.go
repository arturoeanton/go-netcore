package main

import (
	"fmt"
	"index/suffixarray"
)

// suffixarray.Index.Lookup returns matching byte offsets in suffix-array order — the
// positions sorted by their suffix data[pos:] lexicographically (a shorter suffix that
// is a prefix of a longer one sorts first), NOT by position. For n > 0 it returns the
// first n in that order; n == 0 or empty query yields nil.
func main() {
	for _, data := range []string{"banana", "mississippi", "abracadabra", "aaaa", "hello world hello", ""} {
		idx := suffixarray.New([]byte(data))
		for _, q := range []string{"a", "ana", "ssi", "abra", "aa", "hello", "z", "l", "issi", ""} {
			fmt.Printf("%q in %q: all=%v n2=%v n1=%v n0=%v\n", q, data,
				idx.Lookup([]byte(q), -1), idx.Lookup([]byte(q), 2),
				idx.Lookup([]byte(q), 1), idx.Lookup([]byte(q), 0))
		}
	}
	idx := suffixarray.New([]byte("hello"))
	fmt.Printf("%s\n", idx.Bytes())
}
