package main

import "fmt"

// A parenthesized increment/decrement target — (m.p)-- — as emitted by generated
// (ragel) state-machine parsers. Same target as m.p--.
type machine struct{ p int }

func main() {
	m := &machine{p: 5}
	(m.p)--
	(m.p)--
	(*&m.p)++
	fmt.Println(m.p)

	arr := []int{10}
	(arr[0])++
	fmt.Println(arr[0])
}
