package main

import "fmt"

// Exercises Go string semantics that must be preserved by GoString:
//   - len(s) counts bytes
//   - indexing yields a byte
//   - range yields (byteIndex, rune)
func main() {
	s := "á€z" // 2 + 3 + 1 = 6 bytes, 3 runes
	fmt.Println("len", len(s))
	fmt.Println("b0", s[0])
	for i, r := range s {
		fmt.Println("rune", i, r)
	}
}
