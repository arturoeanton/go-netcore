package main

type Counter struct{ N int }

func (c *Counter) Inc()      { c.N = c.N + 1 }
func (c *Counter) Add(d int) { c.N = c.N + d }
func (c Counter) Get() int   { return c.N }

func main() {
	c := Counter{N: 0}
	c.Inc()
	c.Inc()
	c.Add(10)
	println(c.Get())
	println(c.N)
}
