package main

import (
	"encoding/csv"
	"errors"
	"fmt"
	"io"
	"strings"
)

// A csv field-count mismatch returns a *csv.ParseError wrapping csv.ErrFieldCount,
// so errors.Is/As work and the ParseError fields (Line/Column/Err) are populated —
// previously the shim returned a plain error, so errors.As failed and pe.Err was nil.
func main() {
	// ReadAll: error stops the read; ParseError is extractable.
	r := csv.NewReader(strings.NewReader("a,b,c\n1,2\n"))
	_, err := r.ReadAll()
	fmt.Println(err)
	fmt.Println(errors.Is(err, csv.ErrFieldCount))
	var pe *csv.ParseError
	fmt.Println(errors.As(err, &pe))
	fmt.Println(pe.Line, pe.Column, pe.StartLine, pe.Err == csv.ErrFieldCount)

	// Read: the field-count error comes back ALONGSIDE the parsed record, then reading recovers.
	r2 := csv.NewReader(strings.NewReader("x,y\n1,2,3\nok,now\n"))
	rec, e := r2.Read()
	fmt.Println(rec, e)
	rec2, e2 := r2.Read()
	fmt.Println(rec2, e2, errors.Is(e2, csv.ErrFieldCount))
	rec3, e3 := r2.Read()
	fmt.Println(rec3, e3)
	_, e4 := r2.Read()
	fmt.Println(e4 == io.EOF)

	// Explicit FieldsPerRecord > 0.
	r3 := csv.NewReader(strings.NewReader("1,2,3\n4,5\n"))
	r3.FieldsPerRecord = 3
	_, ea := r3.ReadAll()
	var pe3 *csv.ParseError
	errors.As(ea, &pe3)
	fmt.Println(pe3.Line, pe3.Err == csv.ErrFieldCount)

	// FieldsPerRecord = -1 disables the check.
	r4 := csv.NewReader(strings.NewReader("1,2\n3,4,5\n"))
	r4.FieldsPerRecord = -1
	recs, eb := r4.ReadAll()
	fmt.Println(recs, eb)
}
