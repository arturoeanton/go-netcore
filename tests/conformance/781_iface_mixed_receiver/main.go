package main

import "fmt"

// A type with MIXED receivers implementing an interface: some methods have a
// pointer receiver, some a value receiver. NewThing returns a *thing, so the
// interface value holds a pointer. Dispatching a VALUE-receiver method (Label,
// Sum) on that pointer-held value requires Go's automatic (*p).M() deref — goclr
// previously passed the GoPtr where the value receiver was expected (InvalidProgram).
// This is exactly loader.fileLoader in OPA (value-receiver Filtered/All next to
// pointer-receiver WithX).
type Thing interface {
	Bump()         // pointer receiver (mutates)
	Label() string // value receiver
	Sum() int      // value receiver
}

type thing struct {
	n     int
	label string
}

func NewThing(label string) Thing { return &thing{label: label} }

func (t *thing) Bump()        { t.n++ }
func (t thing) Label() string { return t.label }
func (t thing) Sum() int      { return t.n * 10 }

func main() {
	t := NewThing("widget") // holds *thing
	t.Bump()
	t.Bump()
	t.Bump()
	fmt.Println("label:", t.Label()) // value-receiver dispatch on a pointer-held value
	fmt.Println("sum:", t.Sum())
}
