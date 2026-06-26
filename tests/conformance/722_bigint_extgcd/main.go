package main

import (
	"fmt"
	"math/big"
)

// big.Int.GCD sets the Bézout coefficients x, y (when non-nil) so that
// a*x + b*y = gcd(a, b) — previously the shim computed the gcd but left x, y zero.
// Plus a sweep of other big.Int operations.
func gcd(a, b int64) {
	x, y := new(big.Int), new(big.Int)
	z := new(big.Int).GCD(x, y, big.NewInt(a), big.NewInt(b))
	chk := new(big.Int).Add(new(big.Int).Mul(big.NewInt(a), x), new(big.Int).Mul(big.NewInt(b), y))
	fmt.Printf("gcd(%d,%d)=%v x=%v y=%v ok=%v\n", a, b, z, x, y, chk.Cmp(z) == 0)
}

func main() {
	for _, p := range [][2]int64{{240, 46}, {46, 240}, {17, 5}, {100, 0}, {0, 100}, {0, 0}, {48, 36}, {-240, 46}, {240, -46}, {-240, -46}, {7, 7}} {
		gcd(p[0], p[1])
	}
	// nil coefficients: just the gcd.
	fmt.Println(new(big.Int).GCD(nil, nil, big.NewInt(54), big.NewInt(24)))

	// Other big.Int ops.
	fmt.Println(new(big.Int).Exp(big.NewInt(4), big.NewInt(13), big.NewInt(497)))
	fmt.Println(new(big.Int).ModInverse(big.NewInt(7), big.NewInt(26)))
	fmt.Println(new(big.Int).ModSqrt(big.NewInt(5), big.NewInt(41)))
	fmt.Println(new(big.Int).Binomial(10, 3), new(big.Int).MulRange(1, 10))
	a := big.NewInt(0xF0)
	fmt.Println(new(big.Int).And(a, big.NewInt(0x3C)), new(big.Int).Or(a, big.NewInt(0x0F)))
	fmt.Println(new(big.Int).Xor(a, big.NewInt(0xFF)), new(big.Int).AndNot(a, big.NewInt(0x30)))
	fmt.Println(a.Bit(7), a.Bit(0), a.BitLen(), new(big.Int).SetBit(big.NewInt(0), 5, 1))
	n := big.NewInt(255)
	fmt.Println(n.Text(2), n.Text(16), n.Text(8))
	fmt.Println(big.NewInt(-5).Sign(), big.NewInt(5).CmpAbs(big.NewInt(-7)), big.Jacobi(big.NewInt(5), big.NewInt(21)))
	fmt.Println(new(big.Int).Sqrt(big.NewInt(1000000)))
}
