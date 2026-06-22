package main

import "fmt"

type inner struct{ v int }

func (i *inner) set(x int) { i.v = x }

type outer struct{ in inner }

func run() int {
	o := &outer{}
	// defer of a pointer-receiver method whose receiver is a struct field reached
	// through a pointer (o.in) — must take the field's address, like the direct call.
	defer o.in.set(99)
	o.in.set(1)
	return o.in.v
}

func main() {
	o := &outer{}
	defer o.in.set(42) // deferred; runs at return
	o.in.set(7)
	fmt.Println("before return:", o.in.v)
	fmt.Println("run():", run())
	// after main's body, the deferred o.in.set(42) runs (no observable print, but must not crash)
}
