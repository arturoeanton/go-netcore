package main

type Point struct{ x, y int }
type Counter struct{ n int }

func (c *Counter) Inc() { c.n++ }

func main() {
	var s Point
	s.x++
	s.x++
	s.y--
	println(s.x, s.y)

	c := &Counter{}
	c.Inc()
	c.Inc()
	c.Inc()
	println(c.n)

	p := &Counter{n: 10}
	p.n++
	p.n--
	p.n++
	println(p.n)

	a := []int{5, 6, 7}
	a[1]++
	a[2]--
	println(a[0], a[1], a[2])

	m := map[string]int{"k": 1}
	m["k"]++
	m["k"]++
	m["new"]++
	println(m["k"], m["new"])

	x := 100
	px := &x
	*px++
	println(x)

	// compound assignment on lvalues
	p.n += 100
	p.n *= 2
	println(p.n)
	s.x += 40
	println(s.x)
	a[0] += 1000
	println(a[0])
	m["k"] += 50
	println(m["k"])
	*px -= 1
	println(x)
}
