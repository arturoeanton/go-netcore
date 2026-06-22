package main

import (
	"bufio"
	"bytes"
	"fmt"
	"strings"
)

func main() {
	r := bufio.NewReader(strings.NewReader("Hello, World!"))

	// Peek doesn't consume.
	p, _ := r.Peek(5)
	fmt.Println("peek:", string(p), "buffered:", r.Buffered())

	// ReadByte after peek returns the peeked bytes.
	b, _ := r.ReadByte()
	fmt.Println("readbyte:", string(b))
	r.UnreadByte()
	b2, _ := r.ReadByte()
	fmt.Println("after unread:", string(b2))

	// Discard skips bytes.
	n, _ := r.Discard(6) // skip "ello, "  (already consumed 'H')
	fmt.Println("discarded:", n)

	// Read the rest.
	rest := make([]byte, 100)
	m, _ := r.Read(rest)
	fmt.Println("rest:", string(rest[:m]))

	// Peek past EOF returns what's available + error.
	r2 := bufio.NewReader(bytes.NewReader([]byte("ab")))
	pe, err := r2.Peek(5)
	fmt.Println("peek-eof:", string(pe), err != nil)
}
