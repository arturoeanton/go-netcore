package main

import "fmt"

// Shift semantics: Go yields 0 when the count reaches the operand's bit width (and an
// arithmetic sign-fill for a signed >>), whereas the CLR masks the count to the width
// (1<<64 would wrongly be 1). This must hold for every width, signedness, and in both the
// expression and compound-assignment forms. (A *negative* shift count is a separate edge:
// Go panics; goclr returns 0 — see LIMITATIONS — so it is not exercised here.)
func main() {
	var u64 uint64 = 1
	var i64 int64 = -5
	var u32 uint32 = 1
	var i32 int32 = -4
	var u8 uint8 = 0xFF
	var i8 int8 = -2
	var s64, s32, s8, s100 uint = 64, 32, 8, 100

	// count == width -> 0 (or sign-fill for signed >>)
	fmt.Println(u64<<s64, u64>>s64)   // 0 0
	fmt.Println(i64>>s64, i64<<s64)   // -1 0
	fmt.Println(u32<<s32, u32>>s32)   // 0 0
	fmt.Println(i32>>s32)             // -1
	fmt.Println(u8<<s8, u8>>s8)       // 0 0
	fmt.Println(i8>>s8)               // -1

	// count > width -> 0 / sign-fill
	fmt.Println(u64<<s100, i64>>s100) // 0 -1

	// in-range counts unaffected
	fmt.Println(u64<<10, i64>>1, u8<<2, i32>>1)

	// compound assignment must follow the same rule
	x := uint64(1)
	x <<= s64
	fmt.Println(x) // 0
	y := uint32(8)
	y >>= s32
	fmt.Println(y) // 0
	z := int64(-8)
	z >>= s64
	fmt.Println(z) // -1
	w := uint8(0xFF)
	w <<= s100
	fmt.Println(w) // 0

	// shift of a struct field (also routed through the guard)
	type T struct{ V uint32 }
	t := T{1}
	t.V <<= s64
	fmt.Println(t.V) // 0

	// positive variable count below width
	var n uint = 3
	fmt.Println(u64<<n, i64>>n)
}
