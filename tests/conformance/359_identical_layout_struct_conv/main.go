package main

import "fmt"

type Inner struct {
	a int
	b string
}
type Outer Inner

func (o *Outer) Sum() string { return fmt.Sprintf("%d/%s", o.a, o.b) }

func main() {
	i := Inner{7, "x"}
	o := Outer(i)
	fmt.Println(o.a, o.b)
	fmt.Println((*Outer)(&i).Sum())
}
