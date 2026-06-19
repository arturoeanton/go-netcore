package main

func makeCounter() func() int {
	count := 0
	return func() int {
		count = count + 1
		return count
	}
}

func main() {
	c := makeCounter()
	println(c())
	println(c())
	println(c())
	d := makeCounter()
	println(d())
	println(c())
}
