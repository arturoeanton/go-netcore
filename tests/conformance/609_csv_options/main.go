package main

import (
	"bytes"
	"encoding/csv"
	"fmt"
	"strings"
)

// csv.Reader honors Comma, Comment, TrimLeadingSpace, LazyQuotes, and FieldsPerRecord;
// csv.Writer honors Comma and UseCRLF.
func dump(recs [][]string) {
	for _, rec := range recs {
		fmt.Print("|")
		for _, f := range rec {
			fmt.Printf("%s|", f)
		}
	}
	fmt.Println()
}

func main() {
	r := csv.NewReader(strings.NewReader("a;b;c\n# skip me\n1;2;3\n"))
	r.Comma = ';'
	r.Comment = '#'
	recs, _ := r.ReadAll()
	dump(recs)

	r2 := csv.NewReader(strings.NewReader("\"a,b\",\"c\nd\",\"e\"\"f\"\n"))
	recs2, _ := r2.ReadAll()
	dump(recs2)

	r3 := csv.NewReader(strings.NewReader("a,  b,   c\n"))
	r3.TrimLeadingSpace = true
	recs3, _ := r3.ReadAll()
	dump(recs3)

	r4 := csv.NewReader(strings.NewReader("a,b,c\n1,2\n"))
	_, err := r4.ReadAll()
	fmt.Println(err)

	r5 := csv.NewReader(strings.NewReader("a,b\n1,2,3\n4\n"))
	r5.FieldsPerRecord = -1
	recs5, _ := r5.ReadAll()
	dump(recs5)

	// Read one at a time
	r6 := csv.NewReader(strings.NewReader("x,y\n1,2\n3,4\n"))
	for {
		rec, e := r6.Read()
		if e != nil {
			break
		}
		fmt.Println(rec)
	}

	r7 := csv.NewReader(strings.NewReader("a\"b,c\n"))
	r7.LazyQuotes = true
	recs7, _ := r7.ReadAll()
	dump(recs7)

	var buf bytes.Buffer
	w := csv.NewWriter(&buf)
	w.Write([]string{"simple", "has,comma", "has\"quote", "has\nnl"})
	w.Write([]string{"a", "b", "c", "d"})
	w.Flush()
	fmt.Printf("%q\n", buf.String())

	var buf2 bytes.Buffer
	w2 := csv.NewWriter(&buf2)
	w2.Comma = '\t'
	w2.UseCRLF = true
	w2.WriteAll([][]string{{"a", "b"}, {"c", "d"}})
	fmt.Printf("%q\n", buf2.String())
}
