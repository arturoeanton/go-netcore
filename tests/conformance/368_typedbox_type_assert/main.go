package main

import "fmt"

// A named non-struct type (typed box) must answer both the single-value and
// comma-ok type assertions from an interface, and the asserted value must compare
// equal to a constant of that type.
type Delim int32

func main() {
	var v interface{} = Delim(']')

	// comma-ok
	if d, ok := v.(Delim); ok {
		fmt.Println("ok:", d == ']', d == '[')
	} else {
		fmt.Println("comma-ok FAILED")
	}

	// single-value
	d := v.(Delim)
	fmt.Printf("single: %c %v\n", d, d == ']')

	// negative: wrong type
	var w interface{} = "str"
	if _, ok := w.(Delim); ok {
		fmt.Println("WRONG: matched")
	} else {
		fmt.Println("correctly not a Delim")
	}
}
