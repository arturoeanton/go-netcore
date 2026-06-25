package main

import (
	"fmt"
	"io"
)

// %T of a composite whose element is a NAMED interface keeps the interface name
// ([]error, map[error]int), while the empty interface still erases to interface {}
// ([]any). The element values still dispatch their own String()/Error() under %v.
func main() {
	fmt.Printf("%T\n", []error{})
	fmt.Printf("%T\n", []any{1, 2})
	fmt.Printf("%T\n", []interface{}{})
	fmt.Printf("%T\n", map[string]error{})
	fmt.Printf("%T\n", map[error]int{})
	fmt.Printf("%T\n", []io.Reader(nil))
	fmt.Printf("%T\n", [3]error{})

	// nested
	fmt.Printf("%T\n", [][]error{})
	fmt.Printf("%T\n", map[string][]any{})

	// %v of a []error still prints/dispatches element errors
	fmt.Printf("%v\n", []error{fmt.Errorf("boom"), nil})
}
