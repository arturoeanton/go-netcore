package main

import "fmt"

// Appending a NAMED value (a named slice) into an interface-element slice must keep
// the value's named-type identity, so a later type switch / %T sees the named type
// and not its bare underlying type. Single-element append previously dropped the tag
// (multi-element append already kept it). This is OPA's parser: it does
// `stmts = append(stmts, body)` where body is an ast.Body (a named []*Expr), and
// ParseBody then type-switches `case Body:`.
type Expr struct{ v int }
type Body []*Expr

func (b *Body) Append(e *Expr) { *b = append(*b, e) }

func parse() []interface{} {
	var stmts []interface{}
	b := Body{}
	b.Append(&Expr{v: 1})
	b.Append(&Expr{v: 2})
	stmts = append(stmts, b) // single-element append of a named slice into []any
	return stmts
}

func main() {
	for _, stmt := range parse() {
		fmt.Printf("type: %T\n", stmt)
		switch s := stmt.(type) {
		case Body:
			fmt.Println("matched Body, len:", len(s))
		default:
			fmt.Printf("no match: %T\n", stmt)
		}
	}
}
