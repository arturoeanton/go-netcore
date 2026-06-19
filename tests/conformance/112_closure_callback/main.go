package main

func apply(f func(int) int, x int) int {
	return f(x)
}

func main() {
	r := apply(func(n int) int { return n + 100 }, 5)
	println(r)
	y := 10
	r2 := apply(func(n int) int { return n + y }, 5)
	println(r2)
}
