package main

import "fmt"

type Acc struct{ vals []int }

func (a *Acc) add(v int) { a.vals = append(a.vals, v) }

type Box struct {
	A   Acc
	tag string
}

func buildLocal(n int) Box {
	var b Box // local value struct; b.A.add is a ptr-receiver method on a value field
	b.tag = "L"
	for i := 0; i < n; i++ {
		b.A.add(i * i)
	}
	return b
}

func buildThroughPtr(p *Box, n int) {
	for i := 0; i < n; i++ {
		p.A.add(i) // value field reached through a pointer
	}
}

func main() {
	b := buildLocal(4)
	fmt.Println(b.A.vals, b.tag)

	p := &Box{tag: "P"}
	buildThroughPtr(p, 3)
	fmt.Println(p.A.vals, p.tag)
}
