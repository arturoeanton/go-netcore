package main

import (
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"fmt"
)

// crypto/elliptic Curve.Params() — the NIST domain parameters (FIPS 186-4). Previously
// Curve.Params() was unregistered (a nil-deref) and a key's Curve always reported P-256.
// Name/BitSize and the P/N/B/Gx/Gy *big.Int constants are deterministic and byte-exact with
// go run; the curve recovered from a generated key reflects its real size.
func dump(c elliptic.Curve) {
	p := c.Params()
	fmt.Println(p.Name, p.BitSize)
	fmt.Printf("P=%x\n", p.P)
	fmt.Printf("N=%x\n", p.N)
	fmt.Printf("B=%x\n", p.B)
	fmt.Printf("Gx=%x\n", p.Gx)
	fmt.Printf("Gy=%x\n", p.Gy)
}

func main() {
	dump(elliptic.P224())
	dump(elliptic.P256())
	dump(elliptic.P384())
	dump(elliptic.P521())

	// The curve carried by an ecdsa key reflects the size it was generated on, not a
	// hard-coded P-256.
	for _, c := range []elliptic.Curve{elliptic.P256(), elliptic.P384(), elliptic.P521()} {
		k, err := ecdsa.GenerateKey(c, rand.Reader)
		if err != nil {
			fmt.Println("gen err:", err)
			continue
		}
		params := k.PublicKey.Curve.Params()
		fmt.Println("key curve:", params.Name, params.BitSize)
		// A point on the curve satisfies the curve equation, so X/Y are within the field.
		fmt.Println("X<P:", k.X.Cmp(params.P) < 0, "Y<P:", k.Y.Cmp(params.P) < 0)
	}
}
