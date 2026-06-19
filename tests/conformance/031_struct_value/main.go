package main

type Counter struct{ N int }

func main() {
	c := Counter{N: 0}
	c.N = 5
	println(c.N)
	c.N = c.N + 10
	println(c.N)
	d := c
	d.N = 99
	println(c.N)
	println(d.N)
}
