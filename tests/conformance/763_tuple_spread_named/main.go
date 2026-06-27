package main

import (
	"fmt"
	"time"
)

// f(g()) — spreading a multi-value return directly into a call — must preserve each element's
// named-type identity when the destination parameter is an interface. Previously a time.Month
// (or any Stringer named type) spread into fmt.Println printed its raw int (7) instead of
// dispatching String() ("July"). Concrete variadics (...int) must NOT wrap.
type Color int

const (
	Red Color = iota
	Green
	Blue
)

func (c Color) String() string { return []string{"Red", "Green", "Blue"}[c] }

func pair() (Color, Color)         { return Green, Blue }
func triple() (int, Color, string) { return 1, Red, "x" }
func nums() (int, int, int)        { return 4, 5, 6 }
func sum(xs ...int) int {
	s := 0
	for _, x := range xs {
		s += x
	}
	return s
}

func main() {
	t := time.Date(2024, 7, 15, 9, 5, 0, 0, time.UTC)

	// Direct spread into fmt's ...any keeps Stringer dispatch.
	fmt.Println(t.Date())  // 2024 July 15
	fmt.Println(t.Clock()) // 9 5 0
	fmt.Println(pair())    // Green Blue
	fmt.Println(triple())  // 1 Red x

	// Spread into a concrete ...int variadic stays numeric (no wrapping).
	fmt.Println(sum(nums())) // 15

	// Same values via intermediate variables (already worked) — must still match.
	y, m, d := t.Date()
	fmt.Println(y, m, d) // 2024 July 15
}
