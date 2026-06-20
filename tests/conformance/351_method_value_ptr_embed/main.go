package main

import "fmt"

type base struct{ n int }

func (b *base) inc()     { b.n++ }
func (b *base) get() int { return b.n }

type wrap struct {
	base
	tag string
}

func main() {
	w := &wrap{base{5}, "x"}
	f := w.base.inc
	f()
	f()
	g := w.base.get
	fmt.Println(g(), w.base.n)
}
