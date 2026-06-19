package main

func fib(n int) int {
	if n < 2 {
		return n
	}
	return fib(n-1) + fib(n-2)
}

func main() {
	for i := 0; i < 10; i++ {
		println(fib(i))
	}
}
