package main

func a() { println("a") }
func b() { println("b") }
func c() { println("c") }

func main() {
	defer a()
	defer b()
	defer c()
	println("body")
}
