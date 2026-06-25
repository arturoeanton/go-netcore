package main

import (
	"encoding/csv"
	"fmt"
	"io"
	"strings"
)

// csv.Reader.Read returns the parsed record ALONGSIDE the "wrong number of fields"
// error (not a nil record), and reading can continue past the error. ReadAll instead
// aborts on the first mismatch.
func main() {
	// default FieldsPerRecord (from first row); mismatching rows still yield their record
	r := csv.NewReader(strings.NewReader("a,b,c\nd,e\nf,g,h\ni,j,k,l\n"))
	for {
		rec, err := r.Read()
		if err == io.EOF {
			break
		}
		fmt.Printf("%q err=%v\n", rec, err)
	}

	// explicit FieldsPerRecord
	r2 := csv.NewReader(strings.NewReader("1,2\n3,4,5\n6,7\n"))
	r2.FieldsPerRecord = 2
	for {
		rec, err := r2.Read()
		if err == io.EOF {
			break
		}
		fmt.Printf("%q err=%v\n", rec, err)
	}

	// FieldsPerRecord = -1 disables the check
	r3 := csv.NewReader(strings.NewReader("a\nb,c\nd,e,f\n"))
	r3.FieldsPerRecord = -1
	for {
		rec, err := r3.Read()
		if err == io.EOF {
			break
		}
		fmt.Printf("%q ", rec)
	}
	fmt.Println()

	// ReadAll aborts (nil + error) on the first mismatch
	r4 := csv.NewReader(strings.NewReader("a,b\nc,d,e\n"))
	all, err := r4.ReadAll()
	fmt.Println(all == nil, err != nil)
}
