package main

import "fmt"

type Node struct {
	Children []int
	tag      string
}

func main() {
	n := &Node{Children: []int{1, 2, 3}, tag: "x"}
	n.Children = nil
	fmt.Println(n.Children == nil, len(n.Children), n.tag)
	var m Node
	m.Children = nil
	fmt.Println(m.Children == nil)
}
