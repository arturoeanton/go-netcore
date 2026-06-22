package main

import (
	"bytes"
	"fmt"
	"io"
	"strings"
)

type counter struct{ n int }

func (c *counter) Write(p []byte) (int, error) { c.n += len(p); return len(p), nil }

func main() {
	// MultiWriter over a mix of shim writers (bytes.Buffer, strings.Builder) and a
	// user io.Writer, plus a nested MultiWriter (which MultiWriter flattens).
	var a, b bytes.Buffer
	var sb strings.Builder
	c := &counter{}
	inner := io.MultiWriter(&b, &sb)
	w := io.MultiWriter(&a, inner, c)

	n, err := w.Write([]byte("hello"))
	fmt.Println("write:", n, err)
	fmt.Println(a.String(), b.String(), sb.String(), c.n)

	// io.WriteString drives multiWriter.WriteString (StringWriter type assertion path).
	m, err := io.WriteString(w, " world")
	fmt.Println("writestring:", m, err)
	fmt.Println(a.String(), b.String(), sb.String(), c.n)

	// Empty MultiWriter: no dispatch, returns len(p), nil.
	e := io.MultiWriter()
	k, err := e.Write([]byte("xyz"))
	fmt.Println("empty:", k, err)
}
