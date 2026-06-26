package main

import (
	"crypto/subtle"
	"fmt"
)

// crypto/subtle constant-time primitives. ConstantTimeByteEq takes uint8 and
// ConstantTimeEq takes int32 (both sub-word Go types that lower to a 32-bit CLR
// value), exercising the shim-parameter-width contract; the rest take Go int.
func main() {
	fmt.Println(subtle.ConstantTimeCompare([]byte("abc"), []byte("abc")))
	fmt.Println(subtle.ConstantTimeCompare([]byte("abc"), []byte("abd")))
	fmt.Println(subtle.ConstantTimeCompare([]byte("abc"), []byte("ab")))
	fmt.Println(subtle.ConstantTimeCompare(nil, nil))

	fmt.Println(subtle.ConstantTimeByteEq(5, 5), subtle.ConstantTimeByteEq(5, 6))
	fmt.Println(subtle.ConstantTimeByteEq(0, 0), subtle.ConstantTimeByteEq(255, 255))
	fmt.Println(subtle.ConstantTimeEq(3, 3), subtle.ConstantTimeEq(3, 4))
	fmt.Println(subtle.ConstantTimeEq(-1, -1), subtle.ConstantTimeEq(2147483647, 2147483647))

	fmt.Println(subtle.ConstantTimeSelect(1, 10, 20), subtle.ConstantTimeSelect(0, 10, 20))
	fmt.Println(subtle.ConstantTimeLessOrEq(3, 5), subtle.ConstantTimeLessOrEq(5, 3), subtle.ConstantTimeLessOrEq(4, 4))

	// ConstantTimeCopy: v==1 copies, v==0 leaves.
	dst := []byte{1, 2, 3, 4}
	subtle.ConstantTimeCopy(1, dst, []byte{9, 8, 7, 6})
	fmt.Println(dst)
	subtle.ConstantTimeCopy(0, dst, []byte{0, 0, 0, 0})
	fmt.Println(dst)

	// XORBytes
	out := make([]byte, 4)
	n := subtle.XORBytes(out, []byte{0xff, 0x0f, 0xaa, 0x55}, []byte{0x0f, 0xff, 0x55, 0xaa})
	fmt.Println(n, out)

	var ran bool
	subtle.WithDataIndependentTiming(func() { ran = true })
	fmt.Println(ran)
}
