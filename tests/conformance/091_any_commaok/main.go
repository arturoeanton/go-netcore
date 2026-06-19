package main

func main() {
	var x any = 7
	if v, ok := x.(int); ok {
		println("int", v)
	}
	if _, ok := x.(string); ok {
		println("string")
	} else {
		println("not string")
	}
	x = "world"
	s, ok := x.(string)
	println(s, ok)
	i, ok2 := x.(int)
	println(i, ok2)
}
