package main

import (
	"bufio"
	"bytes"
	"fmt"
	"strings"
)

// fmt.Fprint/Fprintf/Fprintln to a *bufio.Writer must append to the buffer (not
// punch straight through to the underlying sink), so their bytes stay ordered
// against WriteString/WriteByte/WriteRune and only reach the sink on Flush.
func main() {
	var sb strings.Builder
	bw := bufio.NewWriter(&sb)
	bw.WriteString("hello ")
	bw.WriteByte('w')
	bw.WriteRune('世')
	fmt.Fprintf(bw, " %d", 42)
	bw.Flush()
	fmt.Printf("%q\n", sb.String())

	// Heavy interleave into a bytes.Buffer
	var buf bytes.Buffer
	w := bufio.NewWriter(&buf)
	fmt.Fprintf(w, "a%d", 1)
	w.WriteString("-mid-")
	fmt.Fprint(w, "b")
	fmt.Fprintln(w, "-end")
	w.WriteByte('Z')
	w.Flush()
	fmt.Printf("%q\n", buf.String())

	// Buffered: nothing reaches the sink before Flush
	var b2 bytes.Buffer
	w2 := bufio.NewWriter(&b2)
	fmt.Fprintf(w2, "buffered %s", "data")
	fmt.Printf("before=%q ", b2.String())
	w2.Flush()
	fmt.Printf("after=%q\n", b2.String())
}
