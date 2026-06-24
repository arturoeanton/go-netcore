package main

import (
	"bytes"
	"encoding/hex"
	"fmt"
	"io"
)

func main() {
	// NewEncoder: stream bytes through, hex-encoded, in two writes.
	var buf bytes.Buffer
	enc := hex.NewEncoder(&buf)
	n1, _ := enc.Write([]byte("Hello"))
	n2, _ := io.WriteString(enc, ", world!")
	fmt.Printf("encoded=%q n1=%d n2=%d\n", buf.String(), n1, n2)

	// Dumper: a hexdump -C style dump, written across two chunks then closed.
	var d bytes.Buffer
	dmp := hex.Dumper(&d)
	dmp.Write([]byte("The quick brown fox jumps "))
	dmp.Write([]byte("over the lazy dog.\x00\x01\xff"))
	dmp.Close()
	fmt.Print(d.String())
	fmt.Println("---")

	// A short, sub-16-byte payload exercises the Close padding path.
	var s bytes.Buffer
	sd := hex.Dumper(&s)
	io.WriteString(sd, "abc")
	sd.Close()
	fmt.Print(s.String())
}
