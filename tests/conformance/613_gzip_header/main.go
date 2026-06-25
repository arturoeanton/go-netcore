package main

import (
	"bytes"
	"compress/gzip"
	"fmt"
	"io"
)

// gzip.Writer.Name/Comment header fields round-trip through gzip.Reader; the
// compression itself stays correct (incl. empty input and multiple writes).
func main() {
	data := bytes.Repeat([]byte("compress me! "), 50)

	var b bytes.Buffer
	w := gzip.NewWriter(&b)
	w.Name = "archive.tar"
	w.Comment = "created by test"
	w.Write(data)
	w.Close()
	r, _ := gzip.NewReader(&b)
	out, _ := io.ReadAll(r)
	fmt.Println(r.Name, "|", r.Comment, "|", bytes.Equal(out, data), "|", b.Len() < len(data))

	// no header fields
	var b2 bytes.Buffer
	w2 := gzip.NewWriter(&b2)
	w2.Write(data)
	w2.Close()
	r2, _ := gzip.NewReader(&b2)
	out2, _ := io.ReadAll(r2)
	fmt.Println(r2.Name == "", r2.Comment == "", bytes.Equal(out2, data))

	// NewWriterLevel + Name
	var b3 bytes.Buffer
	w3, _ := gzip.NewWriterLevel(&b3, gzip.BestSpeed)
	w3.Name = "fast.bin"
	w3.Write(data)
	w3.Close()
	r3, _ := gzip.NewReader(&b3)
	out3, _ := io.ReadAll(r3)
	fmt.Println(r3.Name, bytes.Equal(out3, data))

	// multiple writes
	var b4 bytes.Buffer
	w4 := gzip.NewWriter(&b4)
	w4.Write([]byte("part1 "))
	w4.Write([]byte("part2 "))
	w4.Write([]byte("part3"))
	w4.Close()
	r4, _ := gzip.NewReader(&b4)
	out4, _ := io.ReadAll(r4)
	fmt.Printf("%q\n", string(out4))

	// empty
	var b5 bytes.Buffer
	w5 := gzip.NewWriter(&b5)
	w5.Name = "empty.dat"
	w5.Close()
	r5, _ := gzip.NewReader(&b5)
	out5, _ := io.ReadAll(r5)
	fmt.Println(r5.Name, len(out5))
}
