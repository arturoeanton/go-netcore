package main

import (
	"crypto/elliptic"
	"fmt"
	"math/big"
)

// crypto/elliptic point arithmetic — IsOnCurve / Add / Double / ScalarMult / ScalarBaseMult,
// implemented in affine coordinates over the prime field (short Weierstrass y² = x³ - 3x + b,
// the point at infinity as (0,0)). Coordinates are deterministic, so they are byte-exact with
// go run for every NIST curve, including P-224 (which needs no .NET key, only the parameters).
func test(name string, c elliptic.Curve) {
	p := c.Params()
	k := []byte{0x12, 0x34, 0x56, 0x78, 0x9a}
	x, y := c.ScalarBaseMult(k)
	fmt.Printf("%s sbm.x=%x\n", name, x)
	fmt.Printf("%s sbm.y=%x\n", name, y)
	fmt.Println(name, "on curve:", c.IsOnCurve(x, y))

	// ScalarMult on the base point equals ScalarBaseMult.
	x2, y2 := c.ScalarMult(p.Gx, p.Gy, k)
	fmt.Println(name, "sm==sbm:", x2.Cmp(x) == 0 && y2.Cmp(y) == 0)

	// (k+1)·G == k·G + G.
	xa, ya := c.Add(x, y, p.Gx, p.Gy)
	k1 := new(big.Int).Add(new(big.Int).SetBytes(k), big.NewInt(1)).Bytes()
	xb, yb := c.ScalarBaseMult(k1)
	fmt.Println(name, "add-consistency:", xa.Cmp(xb) == 0 && ya.Cmp(yb) == 0)

	// Double(G) == 2·G, and the base point is on the curve while (1,1) is not.
	dx, dy := c.Double(p.Gx, p.Gy)
	ddx, ddy := c.ScalarBaseMult([]byte{2})
	fmt.Println(name, "double==2G:", dx.Cmp(ddx) == 0 && dy.Cmp(ddy) == 0)
	fmt.Println(name, "G/on, (1,1)/off:", c.IsOnCurve(p.Gx, p.Gy), c.IsOnCurve(big.NewInt(1), big.NewInt(1)))
}

func main() {
	test("P224", elliptic.P224())
	test("P256", elliptic.P256())
	test("P384", elliptic.P384())
	test("P521", elliptic.P521())

	// Scalar 0 yields the point at infinity, reported as (0,0).
	x, y := elliptic.P256().ScalarBaseMult([]byte{0})
	fmt.Println("zero scalar:", x.Sign(), y.Sign())

	// Adding a point to its negation yields the point at infinity.
	p := elliptic.P256().Params()
	negGy := new(big.Int).Sub(p.P, p.Gy)
	ix, iy := elliptic.P256().Add(p.Gx, p.Gy, p.Gx, negGy)
	fmt.Println("P+(-P):", ix.Sign(), iy.Sign())
}
