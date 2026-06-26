package main

import (
	"errors"
	"fmt"
)

type Color int

func (c Color) String() string { return [...]string{"Red", "Green", "Blue"}[c] }

type Celsius float64

func (c Celsius) String() string { return fmt.Sprintf("%.1f°C", float64(c)) }

// Two fmt rules:
//  1. The Stringer/error rule applies to %x/%X/%q (not just %v/%s): a value with
//     String()/Error() formats its string form, which is then hex-encoded or quoted.
//  2. %T renders the builtin aliases via reflect: byte→uint8, rune→int32 in composites.
func main() {
	// Stringer under x/X/q hex-encodes / quotes the String() output.
	fmt.Printf("%x %X %q\n", Color(1), Color(1), Color(1)) // hex of "Green"
	fmt.Printf("%x %q\n", Celsius(36.6), Celsius(36.6))

	// error under x/X/q uses Error(); %v/%s unchanged.
	err := errors.New("boom")
	fmt.Printf("%x %X %q | %v %s\n", err, err, err, err, err)

	// A named/plain []byte (no Stringer) still hex-encodes its bytes.
	type Raw []byte
	r := Raw("Hi")
	fmt.Printf("%x %q %s\n", r, r, r)
	fmt.Printf("%x %q\n", []byte("Hi"), []byte("Hi"))

	// Plain integers are unaffected (numeric hex / rune quote).
	fmt.Printf("%x %X %q\n", 255, 255, 65)

	// A slice of Stringers under %x.
	fmt.Printf("%x\n", []Color{0, 1, 2})

	// %T of byte/rune composites uses uint8/int32.
	fmt.Printf("%T %T %T %T\n", []byte{1}, []rune{1}, [3]byte{}, map[byte]rune{})
	fmt.Printf("%T\n", struct {
		B []byte
		R []rune
		S string
	}{})
}
