package main

import "fmt"

type counter struct{ v int64 }

func (c *counter) inc()       { c.v++ }
func (c *counter) get() int64 { return c.v }

type state struct {
	hits    counter
	total   int
	name    string
	enabled bool
}

var st state

func main() {
	st.name = "global"         // field write on a global struct
	st.enabled = true          // bool field write
	st.total = 5               // int field write
	st.total += 10             // op= on a global field
	st.total++                 // incDec on a global field
	st.hits.inc()              // ptr-receiver method on a field of a global
	st.hits.inc()
	st.hits.v += 3             // op= reaching into a global's nested field
	fmt.Println(st.name, st.enabled, st.total, st.hits.get())
}
