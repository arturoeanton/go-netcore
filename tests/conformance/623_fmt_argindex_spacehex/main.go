package main

import "fmt"

// Two fmt features: explicit argument indexes %[n] (selecting the nth argument for a
// verb, a '*' width, or a '*' precision, with the implicit counter continuing from
// there), and the space flag on %x/%X of a byte string/slice (a space between each
// byte pair).
func main() {
	// explicit argument indexes
	fmt.Printf("%[2]d %[1]d\n", 11, 22)
	fmt.Printf("%[3]d %[1]d %[2]d\n", 1, 2, 3)
	fmt.Printf("%[1]d %[1]o %[1]x\n", 15)
	fmt.Printf("%[1]v is %[1]T\n", 42)
	fmt.Printf("%[1]d %d %d\n", 1, 2, 3) // index then implicit continues
	fmt.Printf("%c%c%[1]c\n", 'a', 'b')  // reuse arg 1
	fmt.Printf("%[2]d=%[1]s\n", "key", 42)
	// [n] selecting the width / precision argument
	fmt.Printf("%[2]*[1]d\n", 3, 5)   // width=arg2(5), value=arg1(3)
	fmt.Printf("%.[1]*f\n", 2, 3.14159) // precision=arg1(2), value=arg2

	// space flag on %x / %X of byte string and slice
	fmt.Printf("% x\n", []byte{0xAB, 0xCD, 0xEF})
	fmt.Printf("% X\n", []byte("Go"))
	fmt.Printf("% x\n", "AB")
	fmt.Printf("%x\n", []byte{1, 2, 255}) // no space flag -> contiguous
	fmt.Printf("% X|%x\n", "Hi", "Hi")
	// space flag on a scalar int %x stays a sign-space, not inter-byte
	fmt.Printf("%x|% x\n", 255, 255)
}
