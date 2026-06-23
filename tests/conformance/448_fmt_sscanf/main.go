package main

import "fmt"

// fmt.Sscanf / Sscan parse text into pointers by verb (or, for Sscan, by the
// pointee type), writing back the exact Go type.
func main() {
	var a, b int
	var s string
	n, err := fmt.Sscanf("12 hello 34", "%d %s %d", &a, &s, &b)
	fmt.Println(n, a, s, b, err)

	var x, y float64
	m, _ := fmt.Sscan("3.14 2.71", &x, &y)
	fmt.Println(m, x, y)

	var hx int
	fmt.Sscanf("ff", "%x", &hx)
	fmt.Println(hx)

	var t bool
	var c int32
	fmt.Sscanf("true 65", "%t %c", &t, &c)
	fmt.Println(t, c)

	var p, q int
	cnt, e := fmt.Sscanf("10,20", "%d,%d", &p, &q)
	fmt.Println(cnt, p, q, e)

	var f32 float32
	fmt.Sscan("1.5", &f32)
	fmt.Println(f32)

	// a parse failure reports how many items were scanned plus an error.
	var bad int
	cn, er := fmt.Sscanf("notanum", "%d", &bad)
	fmt.Println(cn, bad, er != nil)

	// negative and signed values, and a literal-matched format.
	var neg int
	var name string
	fmt.Sscanf("user=-42", "user=%d", &neg)
	fmt.Sscanf("Alice", "%s", &name)
	fmt.Println(neg, name)
}
