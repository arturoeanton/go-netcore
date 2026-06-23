package main

import (
	"fmt"
	"time"
)

// Elements of a slice/array/map whose element type is a Stringer (an enum, a
// time.Month, …) dispatch their String() under %v/%s/%q and Println, even though
// the elements are stored as bare underlying values.
type Color int

func (c Color) String() string { return []string{"red", "green", "blue"}[c] }

type Status int

func (s Status) String() string {
	if s == 0 {
		return "off"
	}
	return "on"
}

func main() {
	cs := []Color{0, 1, 2}
	fmt.Println(cs)
	fmt.Printf("%v %s %q\n", cs, cs, cs)

	ms := []time.Month{time.January, time.March}
	fmt.Println(ms)

	mp := map[string]Status{"a": 0, "b": 1}
	fmt.Println(mp)

	arr := [3]Color{2, 1, 0}
	fmt.Println(arr)

	nest := [][]Color{{0}, {1, 2}}
	fmt.Println(nest)

	// []byte must stay numeric/string, and a plain []int is unaffected.
	bs := []byte("hi")
	fmt.Printf("%v %s\n", bs, bs)
	fmt.Println([]int{1, 2, 3})
}
