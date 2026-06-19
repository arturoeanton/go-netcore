package main

type Vec struct{ X, Y int }

func add(a, b Vec) Vec {
	return Vec{X: a.X + b.X, Y: a.Y + b.Y}
}

func scale(v Vec, k int) Vec {
	v.X = v.X * k
	v.Y = v.Y * k
	return v
}

func main() {
	a := Vec{1, 2}
	b := Vec{3, 4}
	c := add(a, b)
	println(c.X, c.Y)
	s := scale(a, 10)
	println(s.X, s.Y)
	println(a.X, a.Y)
}
