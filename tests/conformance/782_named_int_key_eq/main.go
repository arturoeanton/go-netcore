package main

import (
	"fmt"
	"slices"
)

// A named integer type used as slice elements / map keys. When such a value crosses
// a generic monomorphization (slices.Contains) it can be boxed as a named value on
// one side and its bare underlying int on the other; the key comparer must still
// compare them equal by value. This is OPA's tokens.Token in the Rego parser
// (parseTermOp uses slices.Contains(values, p.s.tok) to detect ==, !=, <, …).
type Token int

const (
	Equal Token = 38
	Neq   Token = 39
	Lt    Token = 40
)

func main() {
	ops := []Token{Equal, Neq, Lt}

	var cur Token = 38 // Equal
	fmt.Println("contains cur:", slices.Contains(ops, cur))
	fmt.Println("contains Neq:", slices.Contains(ops, Neq))
	fmt.Println("contains 99:", slices.Contains(ops, Token(99)))
	fmt.Println("index Lt:", slices.Index(ops, Lt))

	// Named-int map keys go through the same comparer.
	m := map[Token]string{Equal: "==", Neq: "!=", Lt: "<"}
	fmt.Println("map cur:", m[cur])
	fmt.Println("map Lt:", m[Lt])
}
