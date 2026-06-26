package main

import (
	"fmt"
	"math/big"
)

// Two fmt fixes:
//  1. *big.Float is a Formatter: %x defaults to 6 hex-mantissa digits (0x1.c00000p+01,
//     not a float64's shortest form), %X is a bad verb, and every verb outside
//     {e E f F g G x b v} bad-verbs as "*big.Float=<Text('g',10)>". big.Int's set is
//     {b o O d s v x X}; outside it bad-verbs as "big.Int=<dec>".
//  2. The %O verb (0o-prefixed octal) is honored for every integer, including big.Int.
func main() {
	f := big.NewFloat(3.5)
	fmt.Printf("%x %X %e %g %v\n", f, f, f, f, f)       // x ok, X bad
	fmt.Printf("%s %d %q %o %c\n", f, f, f, f, f)        // all bad verbs
	fmt.Printf("%x %.3x\n", big.NewFloat(255), big.NewFloat(255))
	fmt.Printf("%x\n", big.NewFloat(1.0/3))

	i := big.NewInt(255)
	fmt.Printf("%b %o %O %d %x %X %v %s\n", i, i, i, i, i, i, i, i) // all ok
	fmt.Printf("%q %e %f %g %c %U\n", i, i, i, i, i, i)            // all bad verbs

	// %O on the plain integer types.
	fmt.Printf("%O %O %O %O\n", 255, -255, 0, uint(64))
	fmt.Printf("%5O|%-6O|%+O|%#o\n", 9, 9, 9, 9)
	fmt.Printf("%O %O\n", big.NewInt(255), big.NewInt(-8))
}
