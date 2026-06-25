package main

import (
	"encoding/csv"
	"fmt"
	"io"
	"strings"
)

// csv.Reader.LazyQuotes: a quote inside a quoted field that is not followed by a
// delimiter is a literal quote (the field stays quoted). And csv.Reader.ReuseRecord
// is accepted (it's a perf hint; Read still returns a usable record each call).
func main() {
	lazy := []string{
		`a,"b"c",d`,
		`"x""y"`,
		`"a"b"c"d"`,
		`nor"mal,field`,
		`"trailing"x`,
		`"c"x,d`,
	}
	for _, in := range lazy {
		r := csv.NewReader(strings.NewReader(in))
		r.LazyQuotes = true
		rec, err := r.Read()
		fmt.Printf("%q err=%v\n", rec, err)
	}

	// well-formed quotes still parse correctly (embedded comma, newline, doubled quote)
	r := csv.NewReader(strings.NewReader("\"hello, world\",\"line\nbreak\",\"quote\"\"in\""))
	rec, _ := r.Read()
	fmt.Printf("%q\n", rec)

	// ReuseRecord across multiple rows
	r2 := csv.NewReader(strings.NewReader("1,2,3\n4,5,6\n7,8,9\n"))
	r2.ReuseRecord = true
	for {
		rec, err := r2.Read()
		if err == io.EOF {
			break
		}
		fmt.Print(rec, " ")
	}
	fmt.Println()

	// ReuseRecord + LazyQuotes together, several rows of equal width
	r3 := csv.NewReader(strings.NewReader("\"a\"x,b\n\"c\"y,d\n"))
	r3.LazyQuotes = true
	r3.ReuseRecord = true
	for {
		rec, err := r3.Read()
		if err == io.EOF {
			break
		}
		fmt.Printf("%q ", rec)
	}
	fmt.Println()
}
