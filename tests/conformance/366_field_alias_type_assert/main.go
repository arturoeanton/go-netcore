package main

import "fmt"

type prop struct{ v int }
type container struct{ p prop }

type Valued interface{ Value() int }

func (p *prop) Value() int { return p.v }

// &c.p (a field alias) must answer type assertion and pointer-receiver interface
// dispatch as a *prop, like any other *prop pointer.
func get(c *container) interface{} {
	c.p.v = 42
	return &c.p
}

func main() {
	c := &container{}
	x := get(c)
	if pp, ok := x.(*prop); ok {
		fmt.Println("asserted *prop:", pp.v)
	} else {
		fmt.Println("FAILED assertion")
	}
	if v, ok := x.(Valued); ok {
		fmt.Println("dispatched Value():", v.Value())
	} else {
		fmt.Println("FAILED dispatch")
	}
}
