package main

import "fmt"

func main() {
	var i8 int8 = 127
	i8++
	var u8 uint8 = 255
	u8++
	var i16 int16 = 32767
	i16++
	var u16 uint16 = 65535
	u16++
	fmt.Println(i8, u8, i16, u16)

	var b byte = 200
	b += 100
	fmt.Println(b)

	// uint8 wraparound (a byte hash)
	var h uint8
	for _, c := range "abcdef" {
		h = h*31 + byte(c)
	}
	fmt.Println(h)

	var s uint8 = 1
	s <<= 9
	var d int8 = -100
	d -= 50
	fmt.Println(s, d)

	// conversions truncate to the destination width
	x := 300
	big := 100000
	fmt.Println(int8(x), uint8(x), uint16(big), int16(big))

	// wider integers keep their natural wrap
	var i32 int32 = 2147483647
	i32++
	var n int = 9223372036854775807
	n++
	var u uint = 0
	u--
	fmt.Println(i32, n, u)
}
