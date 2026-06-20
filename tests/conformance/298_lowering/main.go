package main

import "fmt"

func main() {
	// append spread
	a := []int{1, 2, 3}
	b := []int{4, 5, 6}
	a = append(a, b...)
	fmt.Println(a, len(a))
	var c []int
	c = append(c, a...)
	fmt.Println(c)
	bs := []byte("ab")
	bs = append(bs, "cd"...)
	fmt.Println(string(bs))

	// local type + const declarations
	type Point struct{ X, Y int }
	p := Point{1, 2}
	fmt.Println(p.X, p.Y, p)
	type MyInt int
	var m MyInt = 5
	fmt.Println(m, m+3)
	const Local = 42
	const A, B = 1, 2
	fmt.Println(Local, A, B)
	type Pair struct{ A, B string }
	pairs := []Pair{{"x", "y"}, {"z", "w"}}
	fmt.Println(pairs)
}
