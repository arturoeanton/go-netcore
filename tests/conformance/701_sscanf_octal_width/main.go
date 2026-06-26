package main

import "fmt"

// fmt.Sscanf now honors the %o (octal), %b (binary), %X and %U verbs, and the field-width
// modifier on %s / %d / %x (%5s reads at most 5 chars, %3d at most 3 digits). Previously
// %o/%b were "bad verb" and the width was skipped.
func main() {
	var o, b, x, X int
	fmt.Sscanf("777 1010 ff FF", "%o %b %x %X", &o, &b, &x, &X)
	fmt.Println(o, b, x, X)

	var a, c int
	fmt.Sscanf("123456", "%3d%3d", &a, &c)
	fmt.Println(a, c)

	var h1, h2 int
	fmt.Sscanf("ffff", "%2x%2x", &h1, &h2)
	fmt.Println(h1, h2)

	var r rune
	fmt.Sscanf("U+4E16", "%U", &r)
	fmt.Printf("%c %d\n", r, r)

	var no int
	fmt.Sscanf("-17", "%o", &no)
	fmt.Println(no)

	var name, rest string
	fmt.Sscanf("John42more", "%4s%s", &name, &rest)
	fmt.Println(name, rest)

	var yr, mo, dy int
	fmt.Sscanf("2024-03-15", "%4d-%2d-%2d", &yr, &mo, &dy)
	fmt.Println(yr, mo, dy)

	// width-limited string in the middle of a format
	var w1, w2 string
	fmt.Sscanf("helloworld", "%5s%5s", &w1, &w2)
	fmt.Println(w1, w2)
}
