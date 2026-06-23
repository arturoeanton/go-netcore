package main

import "fmt"

// %T of a non-nil pointer names its pointee precisely: *Color (not *int), *[]int
// (not *[]interface {}), *main.Pt, and *map[string]int.
type Color int

func (c Color) String() string { return "x" }

type Pt struct{ X int }

func main() {
	x := 5
	fmt.Printf("%T\n", &x)

	var col Color = 1
	fmt.Printf("%T\n", &col)

	fmt.Printf("%T\n", &Pt{1})

	s := []int{1, 2}
	fmt.Printf("%T\n", &s)

	m := map[string]int{"a": 1}
	fmt.Printf("%T\n", &m)

	// stored into an interface first, then %T
	var i interface{} = &col
	fmt.Printf("%T\n", i)

	// the pointer still type-asserts and derefs correctly after tagging
	if p, ok := i.(*Color); ok {
		fmt.Println(*p)
	}
}
