package main

import (
	"fmt"
	"slices"
)

// A multi-element append (spread or literal) must compute the result length once and
// reallocate up front when it exceeds cap, exactly like Go — appending the elements one
// at a time can clobber a slot still aliased by another sub-slice before the cap is
// exceeded.
func main() {
	// slices.Insert exercises the spread realloc path: append(s[:i], make(S, ...)...).
	a := []int{1, 2, 5}
	fmt.Println(slices.Insert(a, 2, 3, 4))     // [1 2 3 4 5]
	fmt.Println(slices.Insert([]int{10, 20}, 0, 7, 8, 9)) // [7 8 9 10 20]

	// Literal multi-element append crossing cap with an aliased sub-slice.
	s := []int{1, 2, 5}
	t := append(s[:2], 3, 4)
	fmt.Println("t:", t, "s:", s, "tail:", s[2:]) // s must stay [1 2 5]

	// Spread append where src aliases dst's region.
	x := []int{1, 2, 3}
	x = append(x, x...)
	fmt.Println("double:", x) // [1 2 3 1 2 3]

	// In-cap multi-append still writes in place (matches Go's shared-backing semantics).
	u := make([]int, 2, 4)
	u[0], u[1] = 9, 8
	v := append(u[:1], 7, 6)
	fmt.Println("v:", v, "u:", u)

	// append to nil and growth caps.
	var n []int
	n = append(n, 1, 2, 3)
	fmt.Println("nil-append:", n, len(n), cap(n))

	// []byte += string... spread.
	b := []byte("ab")
	b = append(b, "cde"...)
	fmt.Println(string(b))

	// Three-element growth on a one-element base.
	g := []int{1}
	g2 := append(g, 2, 3, 4)
	fmt.Println(g2, len(g2), cap(g2), "orig:", g)
}
