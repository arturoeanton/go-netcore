package main

type Point struct{ X, Y int }

func main() {
	p := Point{X: 3, Y: 4}
	println(p.X)
	println(p.Y)
	q := Point{10, 20}
	println(q.X, q.Y)
}
