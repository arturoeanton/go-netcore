// A *T pointer satisfies T's value-receiver methods, so an interface may hold the
// pointer form. Dispatch must match the pointer and deref it to the value receiver.
package main

type Stringer interface{ String() string }

type point struct{ x, y int }

func (p point) String() string { return "pt" } // value receiver

func main() {
	var a Stringer = point{1, 2} // value in interface
	var b Stringer = &point{3, 4} // pointer in interface (satisfies value method)
	println(a.String())
	println(b.String())
}
