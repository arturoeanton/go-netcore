package main

import "fmt"

// A []byte formats as its string form for %s and %q (and hex for %x), like Go,
// rather than as a slice of byte values — the typed box carries []uint8 so a byte
// slice is distinguishable from []int.
func main() {
	b := []byte("hello world")
	fmt.Printf("%s|%q|%x|%X\n", b, b, b, b)
	fmt.Printf("%v\n", b) // %v of []byte stays the numeric slice form
	fmt.Printf("%s\n", []byte(`{"ok":true,"n":42}`))
	fmt.Printf("%q\n", []byte("tab\there"))

	var i interface{} = []byte("through-iface")
	fmt.Printf("%s %q\n", i, i)

	// non-byte slices still recurse / use their own forms
	fmt.Printf("%q %q\n", []string{"a", "b"}, []byte("c"))
	fmt.Printf("%x\n", []int{255, 16})
}
