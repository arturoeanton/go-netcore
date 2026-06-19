package main

func add(a, b int) int { return a + b }
func mul(a, b int) int { return a * b }

func main() {
	println(add(2, 3))
	println(mul(6, 7))
	println(add(mul(2, 3), 4))
	println(100 - 58)
	println(17 % 5)
	println(20 / 3)
}
