package main

import (
	"bufio"
	"bytes"
	"fmt"
	"io"
	"strings"
)

// io.WriteString must write a Go string's raw bytes, even when the string holds
// non-UTF-8 bytes (e.g. a binary signature like the PNG header "\x89PNG\r\n\x1a\n").
// Previously the shim UTF-8-decoded the string, mangling 0x89 to U+FFFD (ef bf bd).
func main() {
	const sig = "\x89PNG\r\n\x1a\n"
	bin := "\x00\x01\x80\xff\xfe\x7f\xc3\x28" // includes invalid UTF-8 (0xc3 0x28)

	// To a bytes.Buffer (StringWriter).
	var b bytes.Buffer
	n, err := io.WriteString(&b, sig)
	fmt.Printf("%x n=%d err=%v\n", b.Bytes(), n, err)

	// To a bufio.Writer wrapping a bytes.Buffer.
	var b2 bytes.Buffer
	bw := bufio.NewWriter(&b2)
	io.WriteString(bw, sig)
	io.WriteString(bw, bin)
	bw.Flush()
	fmt.Printf("%x\n", b2.Bytes())

	// To a strings.Builder is text-only in Go; instead round-trip binary through Buffer.
	var b3 bytes.Buffer
	io.WriteString(&b3, bin)
	fmt.Printf("%x len=%d\n", b3.Bytes(), b3.Len())

	// Reading the bytes back preserves them.
	got, _ := io.ReadAll(strings.NewReader(b3.String()))
	fmt.Printf("%x equal=%t\n", got, string(got) == bin)

	// A plain (non-StringWriter) io.Writer goes through Write([]byte) — still raw.
	var sink rawWriter
	io.WriteString(&sink, sig)
	fmt.Printf("%x\n", sink.data)

	// ASCII still works unchanged.
	var b4 bytes.Buffer
	io.WriteString(&b4, "hello world")
	fmt.Println(b4.String())
}

type rawWriter struct{ data []byte }

func (w *rawWriter) Write(p []byte) (int, error) { w.data = append(w.data, p...); return len(p), nil }
