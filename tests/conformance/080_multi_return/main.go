package main

func divmod(a, b int) (int, int) {
	return a / b, a % b
}

func minmax(a, b int) (int, int) {
	if a < b {
		return a, b
	}
	return b, a
}

func main() {
	q, r := divmod(17, 5)
	println(q, r)
	lo, hi := minmax(8, 3)
	println(lo, hi)
	a, b := divmod(100, 7)
	println(a, b)
}
