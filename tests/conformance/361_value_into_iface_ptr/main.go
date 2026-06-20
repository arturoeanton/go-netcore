package main

import "fmt"

type Inst interface{ tag() string }
type loadA struct{ n int }

func (loadA) tag() string { return "A" }

type loadB struct{ n int }

func (loadB) tag() string { return "B" }

func main() {
	var i Inst = loadA{1}
	p := &i
	*p = loadB{2}
	fmt.Println(i.tag())
	switch (*p).(type) {
	case loadB:
		*p = loadA{9}
	}
	fmt.Println(i.tag())
}
