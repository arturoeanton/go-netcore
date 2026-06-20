package main

import "fmt"

type T struct{ x int }
type base struct {
	c      *T
	offset int
}

func (b *base) init(c *T, off int) { b.c = c; b.offset = off }

type derived struct {
	base
	name string
}

func main() {
	c := &T{x: 5}
	r := &derived{name: "n"}
	r.init(c, 7)
	fmt.Println(r.c == nil, r.offset, r.name)
	fmt.Println(r.c.x)
}
