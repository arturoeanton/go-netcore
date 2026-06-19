package main

type Shape interface {
	Area() int
	Name() string
}

type Square struct{ Side int }

func (s Square) Area() int    { return s.Side * s.Side }
func (s Square) Name() string { return "square" }

type Rect struct{ W, H int }

func (r Rect) Area() int    { return r.W * r.H }
func (r Rect) Name() string { return "rect" }

func describe(s Shape) {
	println(s.Name(), s.Area())
}

func main() {
	describe(Square{Side: 4})
	describe(Rect{W: 3, H: 5})
}
