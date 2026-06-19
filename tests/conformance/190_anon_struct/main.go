package main

func main() {
	var s struct {
		x int
		y string
	}
	s.x = 5
	s.y = "hi"
	s.x++
	println(s.x, s.y)

	p := struct{ a, b int }{a: 1, b: 2}
	println(p.a, p.b)

	q := p
	q.a = 99
	println(p.a, q.a)

	items := []struct {
		name  string
		count int
	}{
		{name: "apple", count: 3},
		{name: "pear", count: 7},
	}
	for _, it := range items {
		println(it.name, it.count)
	}
}
