package main

import "fmt"

// Go 1.20 slice-to-array-pointer conversion (*[N]T)(s): a pointer to the slice's
// backing viewed as a fixed-size array, sharing storage. This is the idiom OPA's
// ast.InterfaceToValue uses to build objects: *(*[2]*Term)(kvs[i : i+2]).
func main() {
	s := []int{10, 20, 30, 40}

	// Convert + deref to read a fixed array value.
	p := (*[2]int)(s[0:2])
	fmt.Println("deref:", (*p)[0], (*p)[1])

	// Shared storage: a write through the array pointer is visible in the slice.
	p[1] = 99
	fmt.Println("shared:", s[1])

	// The exact OPA idiom: rebuild pairs from a flat slice.
	tuples := make([][2]int, len(s)/2)
	for i := 0; i < len(s); i += 2 {
		tuples[i/2] = *(*[2]int)(s[i : i+2])
	}
	fmt.Println("tuples:", tuples)
}
