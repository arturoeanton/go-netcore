package main

import (
	"fmt"
	"slices"
	"strings"
)

func main() {
	// strings iterator helpers (Go 1.24), now consumable via range-over-func.
	for part := range strings.SplitSeq("a,b,c,d", ",") {
		fmt.Print(part, " ")
	}
	fmt.Println()
	for line := range strings.Lines("one\ntwo\nthree\n") {
		fmt.Printf("%q ", line)
	}
	fmt.Println()
	for f := range strings.FieldsSeq("  alpha   beta gamma ") {
		fmt.Print(f, "|")
	}
	fmt.Println()

	// slices iterators, including Backward and Sorted over a Seq.
	for i, v := range slices.Backward([]int{10, 20, 30}) {
		fmt.Printf("[%d]=%d ", i, v)
	}
	fmt.Println()
	sorted := slices.Sorted(slices.Values([]int{4, 1, 3, 2}))
	fmt.Println(sorted)

	// break out of a stdlib iterator early.
	first := ""
	for w := range strings.SplitSeq("quick brown fox", " ") {
		first = w
		break
	}
	fmt.Println("first:", first)
}
