package main

import "fmt"

type Point struct{ X, Y int }
type Seg struct {
	A, B Point
	Tag  string
}

func main() {
	// struct equality is field-wise by value
	a, b, c := Point{1, 2}, Point{1, 2}, Point{3, 4}
	fmt.Println(a == b, a == c, a != b, a != c)

	// nested structs
	s1 := Seg{Point{0, 0}, Point{1, 1}, "x"}
	s2 := Seg{Point{0, 0}, Point{1, 1}, "x"}
	s3 := Seg{Point{0, 0}, Point{1, 2}, "x"}
	fmt.Println(s1 == s2, s1 == s3)

	// fixed-array equality is element-wise
	p, q, r := [3]int{1, 2, 3}, [3]int{1, 2, 3}, [3]int{1, 0, 3}
	fmt.Println(p == q, p == r)

	// array of structs
	xs := [2]Point{{1, 1}, {2, 2}}
	ys := [2]Point{{1, 1}, {2, 2}}
	zs := [2]Point{{1, 1}, {9, 9}}
	fmt.Println(xs == ys, xs == zs)

	// struct as a map key (value-keyed)
	m := map[Point]int{{0, 0}: 10, {1, 1}: 20}
	fmt.Println(m[Point{0, 0}], m[Point{1, 1}])

	// nil-slice comparisons unaffected
	var sl []int
	fmt.Println(sl == nil, []int{1, 2} != nil)
}
