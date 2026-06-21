// Package-level variables must initialize in dependency order, not source order:
// `root` is declared before the children it references, so the children must be
// initialized first (Go's spec). Source-order init would dereference a nil child.
package main

type node struct {
	name     string
	children []*node
	parent   *node
}

func newNode(name string, children ...*node) *node {
	n := &node{name: name, children: children}
	for _, c := range children {
		c.parent = n
	}
	return n
}

var root = newNode("root", a, b)
var a = newNode("a")
var b = newNode("b", b1)
var b1 = newNode("b1")

func main() {
	println(root.name, len(root.children))
	println(root.children[1].children[0].parent.name)
	println(a.parent.name, b.parent.name)
}
