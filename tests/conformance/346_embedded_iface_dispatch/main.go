package main

import "fmt"

type Node interface {
	Idx() int
	Kind() string
}

type Leaf struct{ v int }

func (l Leaf) Idx() int     { return l.v }
func (l Leaf) Kind() string { return "leaf" }

// Wrapper and Chain satisfy Node only via an embedded Node interface.
type Wrapper struct{ Node }
type Chain struct{ Node }

func show(n Node) string { return fmt.Sprintf("%s#%d", n.Kind(), n.Idx()) }

func main() {
	nodes := []Node{
		Leaf{v: 1},
		Wrapper{Node: Leaf{v: 2}},
		Chain{Node: Wrapper{Node: Leaf{v: 3}}},
	}
	for _, n := range nodes {
		fmt.Println(show(n))
	}
}
