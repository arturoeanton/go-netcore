package main

// Assemble ecdsa/rsa keys by field assignment (the pattern jwx and other pure-Go
// crypto code uses) and confirm they round-trip: the components read back, and a
// sign/verify with the assembled key succeeds. All printed values are booleans or
// fixed strings, so the output is identical regardless of the random key material.

import (
	"crypto"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/rsa"
	"crypto/sha256"
	"fmt"
	"math/big"
)

func main() {
	gen, _ := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)

	var priv ecdsa.PrivateKey
	priv.Curve = elliptic.P256()
	priv.X = new(big.Int).Set(gen.X)
	priv.Y = new(big.Int).Set(gen.Y)
	priv.D = new(big.Int).Set(gen.D)

	fmt.Println("ec curve:", priv.Curve.Params().Name)
	fmt.Println("ec X match:", priv.X.Cmp(gen.X) == 0)
	fmt.Println("ec D match:", priv.D.Cmp(gen.D) == 0)

	sum := sha256.Sum256([]byte("assembled-key round-trip"))
	r, s, _ := ecdsa.Sign(rand.Reader, &priv, sum[:])
	fmt.Println("ec verify:", ecdsa.Verify(&priv.PublicKey, sum[:], r, s))

	// ecdsa -> ecdh conversion (Go 1.20) preserves the public point size.
	ep, _ := priv.PublicKey.ECDH()
	fmt.Println("ec ecdh pubLen:", len(ep.Bytes()))

	rgen, _ := rsa.GenerateKey(rand.Reader, 2048)
	var rp rsa.PrivateKey
	rp.N = new(big.Int).Set(rgen.N)
	rp.E = rgen.E
	rp.D = new(big.Int).Set(rgen.D)
	rp.Primes = []*big.Int{new(big.Int).Set(rgen.Primes[0]), new(big.Int).Set(rgen.Primes[1])}

	fmt.Println("rsa N match:", rp.N.Cmp(rgen.N) == 0)
	fmt.Println("rsa E:", rp.E)
	fmt.Println("rsa size:", rp.Size())

	sig, _ := rsa.SignPKCS1v15(rand.Reader, &rp, crypto.SHA256, sum[:])
	fmt.Println("rsa verify:", rsa.VerifyPKCS1v15(&rp.PublicKey, crypto.SHA256, sum[:], sig) == nil)
}
