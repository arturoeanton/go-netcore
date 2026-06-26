package main

import (
	"fmt"
	"math/big"
)

// *big.Int implements fmt.Formatter, so under %v it honors the '+' flag as a sign
// for a non-negative value (a plain scalar's %+v adds no sign — that's the struct
// field-name directive). Covers %+v standalone and as a struct/slice/map field.
func main() {
	fmt.Printf("[%+v][%v][%+d]\n", big.NewInt(42), big.NewInt(42), big.NewInt(42))
	fmt.Printf("[%+v][%+v][%+v]\n", big.NewInt(-5), big.NewInt(0), big.NewInt(1000000))

	type Money struct {
		Cents   *big.Int
		Balance *big.Int
	}
	m := Money{big.NewInt(7), big.NewInt(-3)}
	fmt.Printf("%v\n", m)
	fmt.Printf("%+v\n", m)

	// In a slice / map.
	fmt.Printf("%+v\n", []*big.Int{big.NewInt(1), big.NewInt(-2), big.NewInt(3)})
	fmt.Printf("%+v\n", map[string]*big.Int{"a": big.NewInt(9)})

	// Plain scalars: %+v adds field names to structs but NO sign to numbers.
	type P struct{ X, Y int }
	fmt.Printf("%+v %+v\n", P{1, 2}, 42)
}
