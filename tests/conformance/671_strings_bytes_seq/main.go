package main

import (
	"bytes"
	"fmt"
	"slices"
	"strings"
)

// Go 1.24 iterator-returning string/byte splitters (iter.Seq): Lines, SplitSeq, SplitAfterSeq,
// FieldsSeq — drained via range or slices.Collect.
func main() {
	var ls []string
	for l := range strings.Lines("a\nb\nc") {
		ls = append(ls, l)
	}
	fmt.Printf("%q\n", ls)

	fmt.Printf("%q\n", slices.Collect(strings.SplitSeq("a,b,c", ",")))
	fmt.Printf("%q\n", slices.Collect(strings.SplitSeq("abc", "")))
	fmt.Printf("%q\n", slices.Collect(strings.SplitAfterSeq("a,b,c", ",")))
	fmt.Printf("%q\n", slices.Collect(strings.FieldsSeq("  foo bar  baz ")))

	fmt.Printf("%q\n", slices.Collect(bytes.SplitSeq([]byte("x|y|z"), []byte("|"))))
	fmt.Printf("%q\n", slices.Collect(bytes.FieldsSeq([]byte("  a b  c "))))

	var bl [][]byte
	for l := range bytes.Lines([]byte("p\nq\n")) {
		bl = append(bl, l)
	}
	fmt.Printf("%v\n", bl)

	// early break over a string iterator
	n := 0
	for range strings.SplitSeq("1,2,3,4,5", ",") {
		n++
		if n == 2 {
			break
		}
	}
	fmt.Println(n)
}
