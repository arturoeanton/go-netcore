package main

import "fmt"

type Node struct {
	V        int
	Children []*Node
}

func sum(n *Node) int {
	t := n.V
	for _, c := range n.Children {
		t += sum(c)
	}
	return t
}

func main() {
	// elided &-pointer literals in a slice of *Node
	root := &Node{V: 1, Children: []*Node{
		{V: 2},
		{V: 3, Children: []*Node{{V: 4}, {V: 5}}},
	}}
	fmt.Println(sum(root))

	// elided pointer literals in a map
	m := map[string]*Node{"a": {V: 10}, "b": {V: 20, Children: []*Node{{V: 1}}}}
	fmt.Println(m["a"].V, m["b"].V, len(m["b"].Children))

	// slice of pointers, positional
	pts := []*struct{ X, Y int }{{1, 2}, {3, 4}}
	fmt.Println(pts[0].X, pts[1].Y)
}
