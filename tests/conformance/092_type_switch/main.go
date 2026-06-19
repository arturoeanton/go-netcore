package main

type Point struct{ X, Y int }

func describe(x any) int {
	switch v := x.(type) {
	case int:
		return v * 2
	case string:
		return len(v)
	case Point:
		return v.X + v.Y
	default:
		return -1
	}
}

func main() {
	println(describe(21))
	println(describe("hello"))
	println(describe(Point{3, 4}))
	println(describe(true))
}
