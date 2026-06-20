package main

import "fmt"

func mutate(arr [3]int) [3]int {
	arr[0] = 99
	return arr
}

type P struct{ X int }

func main() {
	// assignment copies the array
	x := [3]int{1, 2, 3}
	y := x
	y[0] = 99
	fmt.Println(x, y)

	// passed to a named function by value
	a := [3]int{1, 2, 3}
	b := mutate(a)
	fmt.Println(a, b)

	// passed to a closure by value
	c := [3]int{4, 5, 6}
	func(arr [3]int) { arr[1] = 0 }(c)
	fmt.Println(c)

	// array of structs copies element values
	ps := [2]P{{1}, {2}}
	qs := ps
	qs[0].X = 99
	fmt.Println(ps[0].X, qs[0].X)

	// stored into a slice (copy), then mutated
	d := [3]int{7, 8, 9}
	box := [][3]int{d}
	box[0][0] = 0
	fmt.Println(d, box[0])

	// slicing an array SHARES its backing (not a copy)
	e := [3]int{1, 2, 3}
	s := e[:]
	s[0] = 99
	fmt.Println(e[0], s[0])
}
