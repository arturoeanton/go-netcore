package main

func name(n int) int {
	switch n {
	case 1, 2:
		return 100
	case 3:
		return 300
	default:
		return -1
	}
}

func main() {
	println(name(1))
	println(name(2))
	println(name(3))
	println(name(9))
	x := 5
	switch {
	case x > 10:
		println("big")
	case x > 3:
		println("mid")
	default:
		println("small")
	}
}
