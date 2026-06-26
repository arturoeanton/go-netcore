package main

import (
	"fmt"
	"math/big"
)

// For integer verbs, precision sets the MINIMUM number of digits (zero-padded),
// distinct from width. The 0 flag is ignored when a precision is given (space-pad
// instead). %.0d of 0 yields no digits. Works via .N, .* and .[i]*, in composites,
// and for *big.Int. The # octal prefix is applied after precision (no double 0).
func main() {
	fmt.Printf("[%.3d][%.3d][%5.3d][%-5.3d][%+.3d][% .3d][%08.3d]\n", 5, -5, 5, 5, 5, 5, 5)
	fmt.Printf("[%.0d][%.0d][%.5x][%.5X][%.4o][%.6b]\n", 0, 7, 255, 255, 8, 5)

	// star precision (plain, and via explicit arg index)
	fmt.Printf("[%.*d][%.[1]*d][%6.*d]\n", 3, 5, 3, 5, 4, 9)

	// the # prefix interacts with precision (prefix added after the zero-pad)
	fmt.Printf("[%#.5x][%#.4o][%#.4o][%#o]\n", 255, 8, 0, 8)

	// composites pad each element by precision
	fmt.Printf("%.3d %5.2d\n", []int{5, 42, 100}, []int{1, 2})
	fmt.Printf("%.4d\n", map[string]int{"a": 7})

	// *big.Int honors precision too
	fmt.Printf("%.5d %.3d %08.3d %-8.3d|\n", big.NewInt(42), big.NewInt(-7), big.NewInt(7), big.NewInt(7))
}
