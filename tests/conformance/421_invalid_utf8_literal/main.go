package main

import "fmt"

// A string literal that is NOT valid UTF-8 must keep its exact bytes: len, byte
// indexing, and range-decoding (invalid bytes -> U+FFFD) all match Go. goclr
// stores such constants byte-lossless rather than round-tripping through UTF-16.
func main() {
	s := "a\xffb\xe4\xb8\xad"
	fmt.Println(len(s), len([]rune(s)))
	for i := 0; i < len(s); i++ {
		fmt.Printf("%d ", s[i])
	}
	fmt.Println()
	for i, r := range s {
		fmt.Printf("%d:%U ", i, r)
	}
	fmt.Println()
	b := []byte(s)
	fmt.Println(len(b), b[1], b[3])
}
