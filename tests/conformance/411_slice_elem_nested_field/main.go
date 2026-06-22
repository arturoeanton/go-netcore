package main

import "fmt"

type C struct{ V int }
type B struct {
	c   C
	tag string
}
type A struct{ b B }

type Inner struct{ X, Y int }
type Node struct {
	u    Inner
	name string
}

func main() {
	nodes := make([]Node, 3)
	nodes[1].u.X = 42
	nodes[1].u.Y = nodes[1].u.X + 1
	nodes[0].name = "zero"
	nodes[2].u = Inner{7, 8}
	fmt.Println(nodes[1].u.X, nodes[1].u.Y, nodes[0].name, nodes[2].u)

	xs := make([]A, 2)
	xs[1].b.c.V = 99
	xs[1].b.tag = "deep"
	fmt.Println(xs[1].b.c.V, xs[1].b.tag)

	var arr [3]B
	p := &arr
	p[2].c.V = 7
	fmt.Println(arr[2].c.V)
}
