package main

import "fmt"

// A nil map keeps its map type through an interface: it prints "map[]" (not <nil>),
// `i == nil` is false for a boxed nil map, and a type assertion recovers a nil map
// that compares == nil and supports reads. Real maps are unaffected.
func main() {
	var m map[string]int
	fmt.Println(m)
	fmt.Printf("%v\n", m)

	var i interface{} = m
	fmt.Println(i == nil)

	m2 := i.(map[string]int)
	fmt.Println(m2 == nil, len(m2), m2["missing"])

	mm := map[string]int{"a": 1, "b": 2}
	fmt.Println(mm, mm == nil)
	var i2 interface{} = mm
	fmt.Println(i2 == nil)

	// nil map passed as an interface argument and re-read.
	show(m)
	show(mm)
}

func show(v interface{}) { fmt.Printf("%v (nil=%v)\n", v, v == nil) }
