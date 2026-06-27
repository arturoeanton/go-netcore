package main

import (
	"crypto/ed25519"
	"crypto/rand"
	"fmt"
)

// crypto/ed25519 — a pure RFC 8032 implementation (the .NET BCL has no Ed25519). Keys and
// signatures are deterministic, so NewKeyFromSeed's public key and Sign's output are
// byte-exact with go run; Verify accepts valid signatures and rejects tampered ones.
func main() {
	seed := make([]byte, ed25519.SeedSize)
	for i := range seed {
		seed[i] = byte(i * 7)
	}
	priv := ed25519.NewKeyFromSeed(seed)
	pub := ed25519.PublicKey(priv[32:])
	msg := []byte("the quick brown fox jumps over the lazy dog")
	sig := ed25519.Sign(priv, msg)

	fmt.Printf("%x\n", pub)
	fmt.Printf("%x\n", sig)
	fmt.Println(ed25519.Verify(pub, msg, sig))
	fmt.Println(ed25519.Verify(pub, []byte("tampered message"), sig))
	fmt.Println(ed25519.Verify(pub, msg, append([]byte{0}, sig[1:]...)))

	// Seed round-trips to the same key.
	fmt.Println(string(ed25519.NewKeyFromSeed(priv.Seed())) == string(priv))
	fmt.Println(priv.Seed()[0], priv.Seed()[31])

	// Several distinct seeds (each deterministic).
	for _, b := range []byte{0, 1, 255, 42} {
		s := make([]byte, 32)
		for i := range s {
			s[i] = b
		}
		k := ed25519.NewKeyFromSeed(s)
		sg := ed25519.Sign(k, msg)
		fmt.Printf("%x %v\n", sg[:8], ed25519.Verify(ed25519.PublicKey(k[32:]), msg, sg))
	}

	// GenerateKey is random, but round-trips.
	gpub, gpriv, err := ed25519.GenerateKey(rand.Reader)
	gsig := ed25519.Sign(gpriv, msg)
	fmt.Println(err == nil, len(gpub), len(gpriv), ed25519.Verify(gpub, msg, gsig))

	fmt.Println(ed25519.SeedSize, ed25519.PublicKeySize, ed25519.PrivateKeySize, ed25519.SignatureSize)
}
