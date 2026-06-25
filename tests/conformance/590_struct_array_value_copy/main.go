package main

import "fmt"

// Copying a struct (or array) value duplicates array fields recursively — they have
// value semantics — while slice/map/pointer fields stay shared.
type Box struct {
	A [2]int
	N int
}
type Outer struct {
	B   Box
	Tag string
}
type WithRef struct {
	P *Box
	S []int
	A [2]int
}

func modify(b Box) { b.A[0] = -1 }
func ret(b Box) Box {
	b.A[1] = 77
	return b
}

func main() {
	// direct struct copy
	x := Box{[2]int{1, 2}, 9}
	y := x
	y.A[0] = 99
	fmt.Println(x, y)

	// argument copy and return copy
	a := Box{[2]int{1, 2}, 0}
	modify(a)
	fmt.Println(a)
	fmt.Println(ret(a), a)

	// nested struct holding a struct holding an array
	o := Outer{Box{[2]int{5, 6}, 1}, "t"}
	p := o
	p.B.A[0] = 42
	fmt.Println(o, p)

	// struct-with-array read out of a slice/map is independent
	s := []Box{{[2]int{1, 1}, 0}}
	w := s[0]
	w.A[0] = 8
	fmt.Println(s[0], w)
	m := map[string]Box{"k": {[2]int{3, 3}, 0}}
	v := m["k"]
	v.A[0] = 4
	fmt.Println(m["k"], v)

	// array of structs deep-copies each element
	var arr [2]Box
	arr[0] = Box{[2]int{1, 2}, 0}
	arr2 := arr
	arr2[0].A[0] = -5
	fmt.Println(arr[0], arr2[0])

	// array of arrays
	var grid [2][2]int
	grid[0] = [2]int{1, 2}
	g2 := grid
	g2[0][0] = 9
	fmt.Println(grid[0], g2[0])

	// pointer/slice fields stay shared across a struct copy
	b := &Box{[2]int{1, 2}, 0}
	r1 := WithRef{P: b, S: []int{9}, A: [2]int{5, 6}}
	r2 := r1
	r2.P.A[0] = 99
	r2.S[0] = 8
	r2.A[0] = -1
	fmt.Println(*r1.P, r1.S, r1.A)
	fmt.Println(*r2.P, r2.S, r2.A)
}
