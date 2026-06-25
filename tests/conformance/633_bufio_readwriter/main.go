package main

import (
	"bufio"
	"fmt"
	"strings"
)

// bufio.ReadWriter promotes the embedded *Reader and *Writer methods; a Reader_*/
// Writer_* shim that receives the ReadWriter must unwrap to the embedded part (it was
// crashing with a cast error), and fmt.Fprint* must buffer through the embedded Writer
// rather than racing ahead of the buffered writes.
func main() {
	var sb strings.Builder
	rw := bufio.NewReadWriter(
		bufio.NewReader(strings.NewReader("read-data here")),
		bufio.NewWriter(&sb))

	// write side: promoted Writer methods + Fprintf, all buffered in order
	rw.WriteString("abc")
	rw.WriteByte('!')
	rw.WriteRune('世')
	fmt.Fprintf(rw, " n=%d", 7)
	rw.Flush()
	fmt.Printf("%q\n", sb.String())

	// read side: promoted Reader methods
	word, _ := rw.ReadString(' ')
	fmt.Printf("%q\n", word)
	b, _ := rw.ReadByte()
	fmt.Printf("%c\n", b)
	pk, _ := rw.Peek(4)
	fmt.Printf("%q\n", pk)

	// plain Writer / Reader still work (no regression)
	var sb2 strings.Builder
	w := bufio.NewWriter(&sb2)
	w.WriteString("plain ")
	w.WriteByte('w')
	fmt.Fprintf(w, " %d", 9)
	w.Flush()
	fmt.Println(sb2.String())

	r := bufio.NewReader(strings.NewReader("line1\nline2"))
	l, _ := r.ReadString('\n')
	fmt.Printf("%q\n", l)
}
