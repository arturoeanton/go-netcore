package main

func main() {
	double := func(x int) int { return x * 2 }
	println(double(5))
	println(double(21))
	greet := func() string { return "hi" }
	println(greet())
}
