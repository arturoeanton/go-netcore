package main

type Point struct{ X, Y int }

func move(p *Point, dx, dy int) {
	p.X = p.X + dx
	p.Y = p.Y + dy
}

func main() {
	p := &Point{X: 1, Y: 2}
	println(p.X, p.Y)
	p.X = 10
	println(p.X, p.Y)
	move(p, 5, 7)
	println(p.X, p.Y)
	q := p
	q.Y = 100
	println(p.Y)
}
