package main

import (
	"bytes"
	"fmt"
	"io"
	"strings"
)

func main() {
	var sb strings.Builder
	sb.WriteString("Hello")
	sb.WriteByte(',')
	fmt.Fprintf(&sb, " %s!", "World")
	fmt.Println(sb.String(), sb.Len())
	sb.Reset()
	sb.WriteString("x")
	fmt.Println(sb.String())

	var buf bytes.Buffer
	fmt.Fprint(&buf, "a=", 1)
	fmt.Fprintln(&buf, " done")
	io.WriteString(&buf, "tail")
	fmt.Println(buf.String(), buf.Len())
}
