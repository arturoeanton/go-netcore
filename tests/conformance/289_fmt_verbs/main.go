package main

import (
	"fmt"
	"math"
)

type Pt struct{ X, Y int }

func main() {
	fmt.Printf("[%d][%5d][%-5d][%05d][%+d][% d]\n", 42, 42, 42, 42, 42, 42)
	fmt.Printf("%x %X %#x %o %#o %b\n", 255, 255, 255, 8, 8, 5)
	fmt.Printf("%d %x\n", uint64(18446744073709551615), uint64(18446744073709551615))
	fmt.Printf("%d %s %v\n", "oops", 42, true)
	fmt.Printf("[%5s][%-5s][%.2s]\n", "ab", "ab", "abcdef")
	fmt.Printf("%f %.2f %8.3f %e %.3e %g\n", 3.14159, 3.14159, 3.14159, 1234.5, 1234.5, 0.0001)
	fmt.Printf("%f %f %g %e\n", math.Inf(1), math.Inf(-1), math.NaN(), math.Inf(1))
	fmt.Printf("%q %q %q\n", "hi\n\t", string([]byte{0xff, 0xfe}), "café")
	fmt.Printf("%T %T %T %T\n", 42, 3.14, "s", true)
	fmt.Printf("%T %T\n", Pt{1, 2}, &Pt{1, 2})
	p := &Pt{1, 2}
	fmt.Printf("%v %+v %#v\n", p, p, p)
	fmt.Printf("%#v\n", Pt{1, 2})
	fmt.Printf("%c%c%c %q\n", 72, 105, 33, 'A')
	fmt.Printf("%*d|%-*d|\n", 6, 7, 6, 7)
	var sl []int
	fmt.Printf("%v %d\n", sl, len(sl))
}
