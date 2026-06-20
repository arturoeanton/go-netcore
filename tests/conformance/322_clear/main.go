package main

import "fmt"

func main() {
	m := map[string]int{"a": 1, "b": 2, "c": 3}
	clear(m)
	fmt.Println(len(m), m == nil)

	s := []int{1, 2, 3, 4}
	clear(s)
	fmt.Println(s, len(s))

	ss := []string{"x", "y", "z"}
	clear(ss[1:])
	fmt.Printf("%q\n", ss)

	m2 := map[int]bool{1: true}
	m2[2] = false
	clear(m2)
	m2[5] = true
	fmt.Println(len(m2), m2[5])
}
