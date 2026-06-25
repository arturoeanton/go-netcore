package main

import "fmt"

// %#v of a nil slice/map renders as `T(nil)`, distinct from an empty `T{}`.
type S struct {
	L []int
	M map[string]int
}

func main() {
	fmt.Printf("%#v\n", []int{})
	fmt.Printf("%#v\n", []int(nil))
	fmt.Printf("%#v\n", map[string]int{})
	fmt.Printf("%#v\n", map[string]int(nil))
	fmt.Printf("%#v\n", []string(nil))
	fmt.Printf("%#v\n", []byte(nil))
	fmt.Printf("%#v\n", []byte{})

	// nil slice field inside a struct keeps its element type.
	fmt.Printf("%#v\n", S{L: []int{1}, M: map[string]int{"a": 1}})
	fmt.Printf("%#v\n", S{L: []int{}, M: map[string]int{}})

	// nested composites with nil inner values.
	fmt.Printf("%#v\n", [][]int{nil, {1}})
	fmt.Printf("%#v\n", map[string][]int{"a": nil})
}
