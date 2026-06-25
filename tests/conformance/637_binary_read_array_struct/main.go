package main

import (
	"bytes"
	"encoding/binary"
	"fmt"
)

// binary.Read into a fixed-size array of structs (or array of arrays) must decode each
// element into a distinct value — previously the elements aliased one instance, so every
// slot showed the last-decoded value.
type Point struct{ X, Y int32 }
type RGB struct{ R, G, B uint8 }
type Header struct {
	Magic   uint32
	Colors  [4]RGB
	Version uint16
}
type Shape struct {
	ID     uint16
	Points [3]Point
	Flags  uint8
	_      uint8
}

func main() {
	// array of two-field structs in a struct
	s := Shape{ID: 42, Points: [3]Point{{1, 2}, {3, 4}, {5, 6}}, Flags: 0xFF}
	var buf bytes.Buffer
	binary.Write(&buf, binary.BigEndian, s)
	var s2 Shape
	binary.Read(&buf, binary.BigEndian, &s2)
	fmt.Println(s2.ID, s2.Points, s2.Flags)

	// array of multi-field uint8 structs
	h := Header{Magic: 0xCAFEBABE, Version: 3, Colors: [4]RGB{{1, 2, 3}, {4, 5, 6}, {7, 8, 9}, {10, 11, 12}}}
	var hb bytes.Buffer
	binary.Write(&hb, binary.BigEndian, h)
	var h2 Header
	binary.Read(&hb, binary.BigEndian, &h2)
	fmt.Println(h2.Magic == h.Magic, h2.Colors, h2.Version)

	// array of arrays
	var grid [2][3]uint8
	binary.Read(bytes.NewReader([]byte{1, 2, 3, 4, 5, 6}), binary.BigEndian, &grid)
	fmt.Println(grid)

	// slice of structs via pointer
	pixels := make([]RGB, 3)
	binary.Read(bytes.NewReader([]byte{100, 101, 102, 110, 111, 112, 120, 121, 122}), binary.BigEndian, &pixels)
	fmt.Println(pixels)

	// slice of scalars via pointer
	nums := make([]uint32, 3)
	binary.Read(bytes.NewReader([]byte{0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3}), binary.BigEndian, &nums)
	fmt.Println(nums)

	// round-trip preserves all distinct elements
	fmt.Println(s2.Points[0] != s2.Points[2], h2.Colors[0] != h2.Colors[3])
}
