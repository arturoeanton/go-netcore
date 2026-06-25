package main

import "fmt"

// A numeric/string/quote verb applied to a struct formats each field with that verb
// (Go recurses the verb into fields); a field the verb doesn't fit is a bad verb.
type P struct{ X, Y int }
type Mixed struct {
	N int
	S string
	B bool
}
type WithStr struct{ A int }

func (w WithStr) String() string { return fmt.Sprintf("W%d", w.A) }

func main() {
	p := P{1, 2}
	fmt.Printf("%d\n", p)
	fmt.Printf("%x\n", p)
	fmt.Printf("%b\n", p)
	fmt.Printf("%o\n", p)
	fmt.Printf("%+d\n", P{1, -2})

	m := Mixed{5, "hi", true}
	fmt.Printf("%d\n", m)
	fmt.Printf("%s\n", m)
	fmt.Printf("%q\n", m)
	fmt.Printf("%x\n", Mixed{255, "AB", false})

	// composites of structs and pointer-to-struct
	fmt.Printf("%d\n", []P{{1, 2}, {3, 4}})
	fmt.Printf("%d\n", map[string]P{"k": {1, 2}})
	type Outer struct {
		P P
		Z int
	}
	fmt.Printf("%d\n", Outer{P{1, 2}, 3})
	fmt.Printf("%d %s\n", &P{9, 8}, &P{9, 8})

	// a Stringer struct: %v/%s use String(), %d still recurses fields
	ws := WithStr{7}
	fmt.Printf("%v %s %d\n", ws, ws, ws)

	// float and rune verbs
	type F struct{ A, B float64 }
	fmt.Printf("%.2f %g\n", F{3.14159, 2.71828}, F{1.5, 2.5})
	type R struct{ A, B rune }
	fmt.Printf("%c\n", R{0x41, 0x42})
}
