package main

import (
	"fmt"
	"os"
)

// The full-slice expression s[lo:hi:max] caps the result's capacity at max-lo, so a
// later append past it reallocates instead of writing into the original's tail.
func ix(d int) int {
	if len(os.Args) > 100 {
		return 99
	}
	return d
}

func try(f func()) {
	defer func() { fmt.Println(recover()) }()
	f()
}

func main() {
	a := []int{1, 2, 3, 4, 5}

	sub := a[1:3:4]
	fmt.Println(len(sub), cap(sub)) // 2 3

	sub = append(sub, 99) // need 3 <= cap 3 -> writes a[3] in place
	fmt.Println(a, sub)   // [1 2 3 99 5] [2 3 99]

	sub = append(sub, 100) // need 4 > cap 3 -> realloc, a unchanged
	fmt.Println(a, sub)    // [1 2 3 99 5] [2 3 99 100]

	// cap == len blocks all in-place aliasing.
	x := []int{0, 0, 0, 0}
	y := x[0:2:2]
	y = append(y, 5)
	fmt.Println(x, y) // [0 0 0 0] [0 0 5]

	// Bounds panics, in Go's check order (cap, then hi, then lo).
	try(func() { _ = a[ix(1):ix(3):ix(9)] }) // [::9] with capacity 5
	try(func() { _ = a[ix(1):ix(6):ix(4)] }) // [:6:4]
	try(func() { _ = a[ix(3):ix(2):ix(4)] }) // [3:2:]

	// Default low with explicit max.
	z := a[:2:3]
	fmt.Println(len(z), cap(z))
}
