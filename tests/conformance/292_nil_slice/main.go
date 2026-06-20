package main

import "fmt"

type T struct {
	A []int
	N string
}

func ret() []int       { return nil }
func take(s []int) bool { return s == nil }

func main() {
	var x []int = nil
	fmt.Println(x == nil, x != nil, len(x))
	var t T
	fmt.Println(t.A == nil)
	t2 := T{A: nil, N: "hi"}
	fmt.Println(t2.A == nil, t2.N)
	fmt.Println(ret() == nil, take(nil), take([]int{1}))
	var z []int
	z = append(z, 1, 2)
	fmt.Println(z, z == nil, len(z))
	x = nil
	fmt.Println(x == nil)
	m := make([][]int, 2)
	fmt.Println(m[0] == nil, m[1] == nil)
}
