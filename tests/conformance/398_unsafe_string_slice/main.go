// The Go 1.20+ unsafe string<->[]byte reinterpret idioms. goclr has no raw memory, so it
// lowers them to a copying conversion (semantically identical; see DESIGN-unsafe-pointer).
package main

import (
	"fmt"
	"unsafe"
)

func b2s(b []byte) string { return unsafe.String(unsafe.SliceData(b), len(b)) }
func s2b(s string) []byte { return unsafe.Slice(unsafe.StringData(s), len(s)) }

func main() {
	fmt.Println(b2s([]byte("hello")))
	bb := s2b("world")
	fmt.Println(string(bb), len(bb))
	fmt.Println(b2s([]byte("café Ω"))) // multibyte
	fmt.Println(len(s2b("")), b2s(nil) == "")
}
