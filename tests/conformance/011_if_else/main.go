package main

func classify(n int) int {
	if n < 0 {
		return -1
	} else if n == 0 {
		return 0
	}
	return 1
}

func main() {
	println(classify(-5))
	println(classify(0))
	println(classify(42))
	x := 7
	if x%2 == 0 {
		println("even")
	} else {
		println("odd")
	}
}
