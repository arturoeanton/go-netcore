package main

import "fmt"

type Inner struct{ A int; B string }
type Box struct {
	I Inner
	S []int
	M map[string]int
}

// fmt of a TOP-LEVEL pointer to a composite expands to &{…}/&[…]/&map[…] like Go,
// regardless of how the pointer was taken — a composite literal, a local var, a
// struct field (&b.I), or an array/slice element (&arr[0]). Previously field- and
// element-address pointers (which alias via a getter, not a held value) printed a
// raw address instead of expanding.
func main() {
	var b Box
	b.I = Inner{1, "x"}
	b.S = []int{1, 2, 3}
	b.M = map[string]int{"k": 9}

	// Field-address pointer to a struct: %v / %+v / %#v.
	fmt.Printf("%v\n", &b.I)
	fmt.Printf("%+v\n", &b.I)
	fmt.Printf("%#v\n", &b.I)

	// Field-address pointer to a slice / map.
	fmt.Printf("%v %v\n", &b.S, &b.M)

	// Array/slice element-address pointer.
	arr := [3]Inner{{4, "a"}, {5, "b"}, {6, "c"}}
	fmt.Printf("%v %+v\n", &arr[1], &arr[1])
	sl := []Inner{{7, "p"}, {8, "q"}}
	fmt.Printf("%v\n", &sl[0])

	// Regression: composite-literal and local-var addresses still expand.
	fmt.Printf("%v %#v\n", &Inner{10, "z"}, &Inner{10, "z"})
	loc := Inner{11, "w"}
	fmt.Printf("%v\n", &loc)

	// Nil typed pointer stays <nil> / (*T)(nil).
	var np *Inner
	fmt.Printf("%v %#v\n", np, np)

	// Pointer to a struct CONTAINING a field pointer is one level; the inner one
	// is nested (address) — avoid printing it to stay deterministic. Just the depth-0 case:
	type Wrap struct{ P *Inner }
	w := Wrap{&b.I}
	_ = w // (nested pointer formatting is non-deterministic; not asserted)
	fmt.Println("done")
}
