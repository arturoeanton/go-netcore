package main

import (
	"bytes"
	"encoding/csv"
	"fmt"
	"strings"
)

// csv.Writer quotes a field per Go's fieldNeedsQuotes: it contains the comma/quote/CR/LF,
// equals the literal "\.", or BEGINS with whitespace (leading space is preserved against
// readers that trim). Round-trips through csv.Reader.
func main() {
	var buf bytes.Buffer
	w := csv.NewWriter(&buf)
	rows := [][]string{
		{"name", "value", "note"},
		{" leading", "trailing ", "  both  "},
		{"\tleadtab", "mid\ttab", "none"},
		{"with\"quote", "new\nline", "ret\rurn"},
		{"\\.", "a,b", ""},
		{"normal", "café", "日本語"},
	}
	w.WriteAll(rows)
	w.Flush()
	fmt.Print(buf.String())
	fmt.Println("---")

	// Round-trip: read it back.
	r := csv.NewReader(strings.NewReader(buf.String()))
	r.FieldsPerRecord = -1
	recs, _ := r.ReadAll()
	for _, rec := range recs {
		fmt.Printf("%q\n", rec)
	}

	// CRLF + custom comma.
	var b2 bytes.Buffer
	w2 := csv.NewWriter(&b2)
	w2.UseCRLF = true
	w2.Comma = ';'
	w2.Write([]string{" a ", "b;c", "d"})
	w2.Flush()
	fmt.Printf("%q\n", b2.String())
}
