package main

import (
	"bytes"
	"encoding/base64"
	"fmt"
	"io"
)

func main() {
	// StdEncoding, written in chunks that straddle 3-byte group boundaries.
	var b1 bytes.Buffer
	e1 := base64.NewEncoder(base64.StdEncoding, &b1)
	e1.Write([]byte("Hel"))
	io.WriteString(e1, "lo, base64 ")
	e1.Write([]byte("world!"))
	e1.Close()
	fmt.Printf("std=%q\n", b1.String())

	// RawURLEncoding (no padding) of bytes that need the URL alphabet (-_).
	var b2 bytes.Buffer
	e2 := base64.NewEncoder(base64.RawURLEncoding, &b2)
	e2.Write([]byte{0xfb, 0xff, 0xbf, 0x00, 0x10})
	e2.Close()
	fmt.Printf("rawurl=%q\n", b2.String())

	// Close with no buffered fringe (exact multiple of 3) and double-Close safety.
	var b3 bytes.Buffer
	e3 := base64.NewEncoder(base64.StdEncoding, &b3)
	e3.Write([]byte("abcdef"))
	e3.Close()
	e3.Close()
	fmt.Printf("exact=%q\n", b3.String())
}
