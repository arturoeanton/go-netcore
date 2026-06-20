package main

import (
	"fmt"
	"unicode"
)

// bitClear exercises the &^ and &^= operators.
func bitClear(a, b uint32) uint32 {
	a &^= 0x0F
	return a &^ b
}

// sumVia exercises &slice[i] (address of a slice element, read and write).
func sumVia(xs []int) int {
	total := 0
	for i := range xs {
		p := &xs[i]
		*p = *p + 1 // mutate through the element pointer
		total += *p
	}
	return total
}

func main() {
	// unicode is compiled from real Go source (RangeTable + Is/In/tables).
	fmt.Println(unicode.IsDigit('7'), unicode.IsLetter('Z'), unicode.IsSpace('\t'))
	fmt.Println(unicode.Is(unicode.Latin, 'A'), unicode.Is(unicode.Latin, 'Ω'))
	fmt.Println(unicode.In('5', unicode.Nd, unicode.Lu))
	fmt.Printf("%c%c\n", unicode.ToLower('K'), unicode.ToUpper('k'))

	fmt.Println(bitClear(0xFF, 0x30))

	xs := []int{10, 20, 30}
	fmt.Println(sumVia(xs), xs)
}
