package main

import "fmt"

// %T of a typed nil pointer names the pointer type precisely, and %v of a nil
// pointer (direct or held in an interface) renders <nil>.
type Color int

func (c Color) String() string { return "x" }

type ML int // method-less named type

type Pt struct{ X int }

func main() {
	var np *int
	fmt.Printf("%T %v\n", np, np)

	var nc *Color
	fmt.Printf("%T %v\n", nc, nc)

	var npt *Pt
	fmt.Printf("%T %v\n", npt, npt)

	var ns *[]int
	fmt.Printf("%T %v\n", ns, ns)

	var nm *map[string]int
	fmt.Printf("%T\n", nm)

	var nml *ML
	fmt.Printf("%T\n", nml)

	// nil pointer stored into an interface, then %T / %v / == nil
	var i interface{} = np
	fmt.Printf("%T %v %v\n", i, i, i == nil)

	// non-nil method-less pointer is precise too
	var ml ML = 5
	fmt.Printf("%T\n", &ml)

	// untyped nil
	fmt.Printf("%T %v\n", nil, nil)
}
