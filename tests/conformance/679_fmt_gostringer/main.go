package main

import "fmt"

// A type implementing fmt.GoStringer controls its %#v (GoSyntax) representation, exactly
// as Go does — at the top level, through a pointer, and nested inside a struct, slice, or
// map. %v / %s are unaffected (those use Stringer or the default). Previously goclr
// ignored GoString() and emitted the default struct syntax.
type Point struct{ X, Y int }

func (p Point) GoString() string { return fmt.Sprintf("Point(%d,%d)", p.X, p.Y) }

type Box struct {
	P    Point
	Name string
}

func main() {
	p := Point{3, 4}
	fmt.Printf("%#v\n", p)  // Point(3,4)
	fmt.Printf("%v\n", p)   // {3 4} — GoString not used for %v
	fmt.Printf("%s\n", p)   // {%!s(int=3) %!s(int=4)}
	fmt.Printf("%#v\n", &p) // &Point(3,4)

	b := Box{Point{1, 2}, "hi"}
	fmt.Printf("%#v\n", b) // nested GoStringer honored

	fmt.Printf("%#v\n", []Point{{5, 6}, {7, 8}})
	fmt.Printf("%#v\n", map[string]Point{"a": {9, 9}})
}
