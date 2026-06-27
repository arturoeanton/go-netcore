package main

import "fmt"

// Signed integer division/remainder by -1 must wrap like Go, not throw: x/-1 is the wrapping
// negation (MinInt/-1 == MinInt) and x%-1 == 0. The CLR throws OverflowException for
// long.MinValue / -1, so goclr guards it. Division by zero still panics (recoverable).
// (Uses runtime variables: a constant MinInt/-1 is a compile-time overflow in Go.)
func main() {
	neg := int64(-1)
	a := int64(-9223372036854775808)
	fmt.Println(a/neg, a%neg) // MinInt64 0

	n32 := int32(-1)
	c := int32(-2147483648)
	fmt.Println(c/n32, c%n32) // MinInt32 0

	n8 := int8(-1)
	e := int8(-128)
	fmt.Println(e/n8, e%n8) // -128 0

	n16 := int16(-1)
	g := int16(-32768)
	fmt.Println(g/n16, g%n16) // -32768 0

	// compound forms route through the same guard
	x := int64(-9223372036854775808)
	x /= neg
	fmt.Println(x)
	y := int32(-2147483648)
	y %= n32
	fmt.Println(y)

	// normal divisions unaffected
	v := 42
	d1 := -1
	fmt.Println(v/d1, -v/d1, 0/d1, v%d1, 17/5, -17%5, 100/-7, 100%-7)

	// struct field /= -1
	type T struct{ V int64 }
	t := T{-9223372036854775808}
	t.V /= neg
	fmt.Println(t.V)

	// divide by zero still panics and is recoverable
	func() {
		defer func() {
			if r := recover(); r != nil {
				fmt.Println("recovered:", r)
			}
		}()
		num, den := 10, 0
		fmt.Println(num / den)
	}()
}
