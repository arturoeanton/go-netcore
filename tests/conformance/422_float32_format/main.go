package main

import "fmt"

// A float32 must be formatted with its 32-bit shortest round-trip, not by widening
// to float64 (which would print the spurious tail, e.g. 0.10000000149011612).
func main() {
	var a float32 = 0.1
	var b float32 = 1.0 / 3.0
	var c float32 = 1e20
	fmt.Println(a, b, c)
	fmt.Printf("%v %g %g\n", a, b, c)
	fmt.Println([]float32{0.1, 0.2, 0.3})
	fmt.Printf("%v %v\n", float32(3.14), float64(float32(0.1)))
}
