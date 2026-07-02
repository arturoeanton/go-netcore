package main

import (
	"crypto/ecdh"
	"encoding/hex"
	"fmt"
)

func main() {
	// RFC 7748 section 6.1 test vectors (deterministic — same on go run and goclr).
	aScalar, _ := hex.DecodeString("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a")
	bScalar, _ := hex.DecodeString("5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb")

	x := ecdh.X25519()
	a, err := x.NewPrivateKey(aScalar)
	fmt.Println("newpriv err:", err)
	b, _ := x.NewPrivateKey(bScalar)

	fmt.Println("aPub:", hex.EncodeToString(a.PublicKey().Bytes()))
	fmt.Println("bPub:", hex.EncodeToString(b.PublicKey().Bytes()))

	ab, _ := a.ECDH(b.PublicKey())
	ba, _ := b.ECDH(a.PublicKey())
	fmt.Println("shared:", hex.EncodeToString(ab))
	fmt.Println("agree:", hex.EncodeToString(ab) == hex.EncodeToString(ba))
	fmt.Println("curve:", a.Curve() == ecdh.X25519())

	// P-256: derive a public point from a fixed scalar, check the uncompressed form.
	scalar := make([]byte, 32)
	for i := range scalar {
		scalar[i] = byte(i + 1)
	}
	p := ecdh.P256()
	c, err := p.NewPrivateKey(scalar)
	fmt.Println("p256 err:", err)
	pub := c.PublicKey().Bytes()
	fmt.Println("p256 pubLen:", len(pub), "tag:", pub[0])
	rt, _ := p.NewPublicKey(pub)
	fmt.Println("p256 roundtrip:", hex.EncodeToString(rt.Bytes()) == hex.EncodeToString(pub))
}
