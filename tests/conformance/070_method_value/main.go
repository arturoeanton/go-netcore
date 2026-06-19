package main

type Rect struct{ W, H int }

func (r Rect) Area() int      { return r.W * r.H }
func (r Rect) Perimeter() int { return 2 * (r.W + r.H) }
func (r Rect) Scaled(k int) Rect {
	return Rect{W: r.W * k, H: r.H * k}
}

func main() {
	r := Rect{W: 3, H: 4}
	println(r.Area())
	println(r.Perimeter())
	s := r.Scaled(2)
	println(s.Area())
	println(r.Area())
}
