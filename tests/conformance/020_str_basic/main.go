package main

func main() {
	a := "foo"
	b := "bar"
	println(a + b)
	println(len(a + b))
	println(a == "foo")
	println(a == b)
	println(a < b)
	println("a" < "b")
	var empty string
	println(len(empty))
}
