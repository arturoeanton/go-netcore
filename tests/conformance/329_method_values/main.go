package main

import "fmt"

type Counter struct{ n int }

func (c *Counter) Inc()           { c.n++ }
func (c *Counter) Add(d int) int  { c.n += d; return c.n }
func (c Counter) Value() int      { return c.n }

func apply(f func() int) int { return f() }

func main() {
	c := &Counter{}
	inc := c.Inc // bound pointer-receiver method value
	inc()
	inc()
	inc()
	fmt.Println(c.Value())

	add := c.Add
	fmt.Println(add(10), add(5))

	val := c.Value // value-receiver method value
	fmt.Println(val())

	// method value on an addressable value with a pointer receiver
	var d Counter
	di := d.Inc
	di()
	di()
	fmt.Println(d.Value())

	// passed as a function value
	fmt.Println(apply(c.Value))

	// collected into a slice and invoked
	ops := []func(){c.Inc, c.Inc}
	for _, op := range ops {
		op()
	}
	fmt.Println(c.Value())
}
