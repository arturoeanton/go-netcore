package main

import (
	"encoding/csv"
	"fmt"
	"io"
	"strings"
)

func main() {
	// Mix: simple fields, a quoted field with an embedded comma, and a
	// quoted field that spans two physical lines, plus a final record.
	src := "a,b,c\n\"x,y\",\"multi\nline\",z\nfoo,bar,baz\n"
	r := csv.NewReader(strings.NewReader(src))
	for {
		rec, err := r.Read()
		if err == io.EOF {
			break
		}
		if err != nil {
			fmt.Println("err:", err)
			break
		}
		fmt.Printf("record %v\n", rec)
		for i := range rec {
			line, col := r.FieldPos(i)
			fmt.Printf("  field %d at line %d col %d\n", i, line, col)
		}
		fmt.Printf("  inputOffset=%d\n", r.InputOffset())
	}
	fmt.Printf("final inputOffset=%d\n", r.InputOffset())
}
