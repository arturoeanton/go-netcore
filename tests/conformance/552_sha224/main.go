package main

import (
	"crypto/sha256"
	"fmt"
)

func main() {
	for _, m := range []string{"", "abc", "The quick brown fox jumps over the lazy dog", "a much longer message designed to span more than one 64-byte SHA-256 block boundary exactly!"} {
		fmt.Printf("%x\n", sha256.Sum224([]byte(m)))
	}
	h := sha256.New224()
	h.Write([]byte("streamed "))
	h.Write([]byte("in two parts"))
	fmt.Printf("stream %x\n", h.Sum(nil))
}
