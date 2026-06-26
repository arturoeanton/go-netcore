package main

import "fmt"

// Core integer/conversion semantics, byte-exact with Go: truncate-toward-zero division and
// modulo with negatives, runtime shifts, fixed-width overflow wraparound, truncating numeric
// conversions, float->int truncation, byte/rune conversions, and bitwise ops (incl. &^).
func main() {
	a, b := -7, 2
	fmt.Println(a/b, a%b, 7/(-b), 7%(-b), -7/(-b), -7%(-b))

	var s uint = 1
	fmt.Println(1<<s, int64(1)<<s<<62, -1>>s)
	var u uint32 = 0xFFFFFFFF
	fmt.Println(u>>16, u<<16)

	var i8 int8 = 127
	i8++
	fmt.Println(i8)
	var u8 uint8 = 255
	u8++
	fmt.Println(u8)

	var big int64 = 300
	var negone int64 = -1
	var huge int64 = 0x1FFFFFFFF
	fmt.Println(int8(big), uint8(negone), int32(huge))
	var v65537, v40000 int = 65537, 40000
	fmt.Println(uint16(v65537), int16(v40000))

	f1, f2, f3 := 3.9, -3.9, 2.5
	fmt.Println(int(f1), int(f2), int(f3))

	var n int64 = 9007199254740993
	fmt.Println(float64(n))

	fmt.Println(string(rune(65)), string(rune(0x4e16)))
	fmt.Println([]byte("AB"), []rune("世"))

	x, y := 0xF0, 0x0F
	fmt.Println(x&y, x|y, 0xFF^y, 0xFF&^y)
}
