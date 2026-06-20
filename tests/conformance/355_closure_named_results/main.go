package main

import "fmt"

func makeAdder() func(int) (sum int) {
	base := 100
	return func(x int) (sum int) {
		sum = base + x
		return
	}
}

func multi() func() (a int, b string) {
	return func() (a int, b string) {
		a = 7
		b = "hi"
		return
	}
}

func main() {
	add := makeAdder()
	fmt.Println(add(5), add(20))
	x, y := multi()()
	fmt.Println(x, y)
}
