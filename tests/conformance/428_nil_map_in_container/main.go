package main

import "fmt"

// A nil map keeps its map identity everywhere — printed as map[], not <nil> — as a
// struct field, a slice element (literal nil or make-zeroed), and after an interface
// round-trip; == nil works for both the null and GoMap{Data:null} representations.
type S struct {
	M map[int]string
	N map[string]int
}

func main() {
	var m map[string]int
	fmt.Println(m, m == nil, m != nil)
	m2 := map[string]int{"a": 1}
	fmt.Println(m2 == nil, m2 != nil)

	fmt.Printf("%v %+v\n", S{}, S{})
	fmt.Println([]map[string]int{nil, {"x": 1}})
	fmt.Println(make([]map[int]int, 2))

	// nil-map reads still work.
	fmt.Println(len(m), m["z"])

	var i interface{} = m
	fmt.Println(i == nil)
	mm := i.(map[string]int)
	fmt.Println(mm == nil, len(mm))
}
