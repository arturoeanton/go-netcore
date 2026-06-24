package main

import (
	"crypto/sha512"
	"fmt"
)

func main() {
	msgs := []string{"", "abc", "The quick brown fox jumps over the lazy dog", "goclr byte-exact sha512/256 and /224 test vector with some length to span blocks!!!!"}
	for _, m := range msgs {
		fmt.Printf("224 %x\n", sha512.Sum512_224([]byte(m)))
		fmt.Printf("256 %x\n", sha512.Sum512_256([]byte(m)))
	}
	// Streaming via New.
	h := sha512.New512_256()
	h.Write([]byte("streamed "))
	h.Write([]byte("in parts"))
	fmt.Printf("stream256 %x\n", h.Sum(nil))
	h2 := sha512.New512_224()
	h2.Write([]byte("streamed in parts"))
	fmt.Printf("stream224 %x\n", h2.Sum(nil))
}
