package main

import (
	"fmt"
	"math/big"
	"math/rand"
)

func main() {
	// ModSqrt across the three prime classes.
	// p=23 (≡3 mod4), p=29 (≡5 mod8), p=17 (≡1 mod8 → Tonelli-Shanks).
	for _, tc := range []struct{ x, p int64 }{{2, 23}, {7, 29}, {2, 17}, {3, 23}, {5, 29}} {
		z := new(big.Int)
		r := z.ModSqrt(big.NewInt(tc.x), big.NewInt(tc.p))
		if r == nil {
			fmt.Printf("sqrt(%d) mod %d = none\n", tc.x, tc.p)
		} else {
			fmt.Printf("sqrt(%d) mod %d = %s (check %s)\n", tc.x, tc.p, z,
				new(big.Int).Mod(new(big.Int).Mul(z, z), big.NewInt(tc.p)))
		}
	}

	// Int.Rand with a seeded rand → deterministic.
	rnd := rand.New(rand.NewSource(1234))
	n := new(big.Int)
	n.SetString("1000000000000000000000000007", 10)
	for i := 0; i < 5; i++ {
		fmt.Println(new(big.Int).Rand(rnd, n))
	}
}
