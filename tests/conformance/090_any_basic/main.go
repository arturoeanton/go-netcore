package main

func describe(x any) int {
	if v, ok := x.(int); ok {
		return v
	}
	return -1
}

func main() {
	var a any = 42
	var b any = "hello"
	n := a.(int)
	println(n)
	s := b.(string)
	println(s)
	println(describe(100))
	println(describe("x"))
	var z any
	println(z == nil)
	z = 5
	println(z == nil)
}
