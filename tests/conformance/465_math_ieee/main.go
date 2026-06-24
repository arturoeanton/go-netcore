package main

import (
	"fmt"
	"math"
)

func main() {
	fmt.Println(math.Dim(4, 3), math.Dim(3, 4))
	fmt.Println(math.FMA(2, 3, 4), math.FMA(0.1, 0.2, 0.3))

	f, e := math.Frexp(8.0)
	fmt.Println(f, e)
	f2, e2 := math.Frexp(0.0)
	fmt.Println(f2, e2)
	f3, e3 := math.Frexp(-12.5)
	fmt.Println(f3, e3)

	fmt.Println(math.Ldexp(0.5, 4), math.Ldexp(1, -1074))
	fmt.Println(math.Ilogb(8), math.Ilogb(0.5), math.Ilogb(0))
	fmt.Println(math.Logb(8), math.Logb(0))
	fmt.Println(math.Nextafter(1, 2), math.Nextafter(1, 0))
	fmt.Println(math.Nextafter32(1, 2), math.Nextafter32(1, 0))
	fmt.Println(math.RoundToEven(2.5), math.RoundToEven(3.5), math.RoundToEven(2.4), math.RoundToEven(-2.5))

	s, c := math.Sincos(1.0)
	fmt.Println(s, c)
	fmt.Println(math.Logb(math.Inf(1)), math.Ilogb(math.Inf(1)))
}
