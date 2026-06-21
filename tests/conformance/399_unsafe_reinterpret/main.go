// The pre-1.20 zero-copy reinterpret idiom *(*T)(unsafe.Pointer(&X)) for the safe
// string<->[]byte directions, lowered as a copying conversion (goclr has no raw memory).
package main

import (
	"fmt"
	"unsafe"
)

func b2s(b []byte) string { return *(*string)(unsafe.Pointer(&b)) }
func s2b(s string) []byte { return *(*[]byte)(unsafe.Pointer(&s)) }

func main() {
	fmt.Println(b2s([]byte("hello")))
	fmt.Println(string(s2b("café Ω")), len(s2b("abc")))
	fmt.Println(b2s(nil) == "")
}
