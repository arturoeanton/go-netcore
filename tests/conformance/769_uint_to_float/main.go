package main

import "fmt"

// Converting an unsigned integer to a float must treat it as unsigned: float64(^uint64(0)) is
// ~1.8e19, not -1. The CLR's conv.r8 is signed, so a uint with the high bit set converted
// negative; goclr now precedes it with conv.r.un for unsigned sources. Signed sources and the
// always-positive uint8/uint16 are unaffected.
func main() {
	var u64 uint64 = 18446744073709551615
	var u64b uint64 = 12000000000000000000
	var u64c uint64 = 9223372036854775808 // MaxInt64 + 1
	var u32 uint32 = 4294967295
	var u32b uint32 = 4000000000
	var u16 uint16 = 60000
	var u8 uint8 = 200
	var u uint = 18446744073709551615
	var up uintptr = 0xFFFFFFFFFFFFFFFF

	fmt.Println(float64(u64), float64(u64b), float64(u64c))
	fmt.Println(float64(u32), float64(u32b), float64(u16), float64(u8))
	fmt.Printf("%g %g\n", float64(u), float64(up))
	fmt.Println(float32(u64), float32(u32))

	// signed sources stay signed (no regression)
	var i64 int64 = -5
	var i32 int32 = -100
	var i8 int8 = -1
	fmt.Println(float64(i64), float64(i32), float64(i8))

	// used inside arithmetic
	fmt.Println(float64(u64c)/2.0, float64(u64)+0.0)
}
