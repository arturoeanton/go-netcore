package main

func greet(name string) string {
	return "hi " + name
}

func firstByte(s string) int {
	return int(s[0])
}

func main() {
	println(greet("bob"))
	println(firstByte("Xyz"))
	println(greet(greet("x")))
}
