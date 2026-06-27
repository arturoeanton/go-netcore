package main

import (
	"fmt"
	"go/token"
	"strings"
	"text/scanner"
)

// go/token.Position and text/scanner.Position share one runtime representation but have
// different String() formats ("file:line:col" / "-" vs "<input>:line:col"). fmt's %v / %s must
// dispatch each type's own String() — previously a scanner Position printed as a raw struct.
func main() {
	// text/scanner positions print as <input>:line:col through %v, %s and String().
	var s scanner.Scanner
	s.Init(strings.NewReader("foo + 42\nbar"))
	for tok := s.Scan(); tok != scanner.EOF; tok = s.Scan() {
		fmt.Printf("%s | %v | %q\n", s.Position, s.Pos(), s.TokenText())
	}

	// go/token positions print as file:line:col, and the zero value as "-".
	fs := token.NewFileSet()
	f := fs.AddFile("main.go", -1, 200)
	for _, off := range []int{0, 10, 50} {
		fmt.Println(fs.Position(f.Pos(off)))
	}
	var z token.Position
	fmt.Printf("%v %s\n", z, z.String())
	fmt.Println(z.IsValid())

	// A zero text/scanner.Position prints <input> (no valid line).
	var sp scanner.Position
	fmt.Printf("%v\n", sp)
}
