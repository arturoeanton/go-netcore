package main

func stats(s string) (int, byte) {
	return len(s), s[0]
}

func main() {
	n, first := stats("hello")
	println(n, first)
	_, c := stats("xyz")
	println(c)
	n2, _ := stats("ab")
	println(n2)
}
