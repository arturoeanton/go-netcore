package main

import (
	"encoding/json"
	"encoding/xml"
	"fmt"
)

// Blob is a method-less named []byte (reaches tagComposite).
type Blob []byte

// Tag is a named []byte WITH a method (reaches namedIdentity).
type Tag []byte

func (t Tag) Len() int { return len(t) }

func main() {
	cd := xml.CharData("hello")
	fmt.Printf("%s|%q|%x|%X\n", cd, cd, cd, cd)

	rm := json.RawMessage(`{"k":1}`)
	fmt.Printf("%s|%q\n", rm, rm)

	var b Blob = []byte("hi")
	fmt.Printf("%s|%q|%x\n", b, b, b)

	tg := Tag("world")
	fmt.Printf("%s|%q|%d\n", tg, tg, tg.Len())

	// %v stays numeric for named []byte (Go's behavior).
	fmt.Printf("%v\n", cd)
	// Plain non-byte slices stay numeric.
	fmt.Printf("%v %x\n", []int{1, 2}, []byte{0xde, 0xad})
}
