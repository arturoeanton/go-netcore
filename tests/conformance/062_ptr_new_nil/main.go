package main

func main() {
	p := new(int)
	println(*p)
	*p = 42
	println(*p)
	var q *int
	println(q == nil)
	q = p
	println(q == nil, *q)
	println(p == q)
}
