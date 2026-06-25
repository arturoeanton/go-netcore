package main

import "fmt"

type Point struct{ X, Y int }
type Tagged struct {
	Name string
	Vals []int
	M    map[string]int
}

// %#v (Go-syntax) details: unsigned ints render in hex (0x5), []byte elements as hex bytes,
// a nil typed pointer as (*int)(nil), and an anonymous struct by its reflect spelling
// ("struct { A int; B string }") for both %#v and %T.
func main() {
	fmt.Printf("%#v\n", Point{1, 2})
	fmt.Printf("%#v\n", &Point{3, 4})
	fmt.Printf("%#v\n", []int{1, 2, 3})
	fmt.Printf("%#v\n", map[string]int{"a": 1})
	fmt.Printf("%#v\n", "hi\tthere")
	fmt.Printf("%#v\n", []byte("hi"))
	fmt.Printf("%#v\n", []byte{0, 255, 16})
	fmt.Printf("%#v\n", uint(5))
	fmt.Printf("%#v\n", uint64(255))
	fmt.Printf("%#v\n", uint32(4096))
	fmt.Printf("%#v\n", 42)
	fmt.Printf("%#v\n", int8(-3))
	fmt.Printf("%#v\n", Tagged{"x", []int{1, 2}, map[string]int{"k": 9}})
	fmt.Printf("%#v\n", []Point{{1, 2}, {3, 4}})

	var p *int
	fmt.Printf("%#v\n", p)

	// anonymous struct: %#v and %T use the reflect spelling
	fmt.Printf("%#v\n", struct {
		A int
		B string
	}{1, "x"})
	fmt.Printf("%T\n", struct{ X int }{5})
	fmt.Printf("%T\n", struct{}{})
	fmt.Printf("%#v\n", struct{}{})
}
