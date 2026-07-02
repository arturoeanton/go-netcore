package main

import "fmt"

type position struct {
	line, col, offset int
}

type cursor struct {
	position // embedded — line/col/offset are promoted
	width    int
}

type scanner struct {
	pt cursor
}

// Pointer-receiver mutation of a promoted field of a nested struct field:
// p.pt.offset (offset is promoted from the embedded position inside pt inside *p).
func (p *scanner) advance() {
	p.pt.offset += p.pt.width // compound assign to a pointer-rooted promoted field
	p.pt.col++                // inc-dec on the same
	if p.pt.offset%3 == 0 {
		p.pt.line++
		p.pt.col = 0 // plain assign to a pointer-rooted promoted field
	}
}

func clear(m map[string]int, k string) {
	defer delete(m, k) // defer of the delete builtin
	m[k] = 99
}

func main() {
	s := &scanner{pt: cursor{width: 2}}
	for i := 0; i < 5; i++ {
		s.advance()
	}
	fmt.Println("offset:", s.pt.offset, "line:", s.pt.line, "col:", s.pt.col)

	m := map[string]int{"a": 1}
	clear(m, "a")
	_, ok := m["a"]
	fmt.Println("deleted:", !ok, "len:", len(m))
}
