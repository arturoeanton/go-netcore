package main

import "math/bits"

func main() {
	println(bits.OnesCount64(0xFF))
	println(bits.LeadingZeros64(1))
	println(bits.TrailingZeros64(8))
	println(bits.Len64(255))
	println(bits.OnesCount(0b1011))
	println(bits.RotateLeft64(1, 4))
	println(bits.TrailingZeros64(0))
}
