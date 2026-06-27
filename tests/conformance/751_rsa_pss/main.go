package main

import (
	"crypto"
	"crypto/rand"
	"crypto/rsa"
	"crypto/sha256"
	"crypto/sha512"
	"fmt"
)

// crypto/rsa SignPSS / VerifyPSS — a from-scratch EMSA-PSS (RFC 8017 §9.1) over the raw RSA
// primitive, since the .NET BCL only offers PSS with salt-length == hash-length. PSS salts are
// random, so signatures are not byte-reproducible; what is deterministic (and matches go run)
// is every verification outcome across Go's three salt-length modes: Auto (max salt),
// EqualsHash (-1), and an explicit count, including the cross-mode accept/reject rules.
func main() {
	key, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		fmt.Println("gen err:", err)
		return
	}
	pub := &key.PublicKey
	msg := []byte("crypto/rsa PSS conformance message")
	h := sha256.Sum256(msg)

	auto := &rsa.PSSOptions{SaltLength: rsa.PSSSaltLengthAuto, Hash: crypto.SHA256}
	eq := &rsa.PSSOptions{SaltLength: rsa.PSSSaltLengthEqualsHash, Hash: crypto.SHA256}
	fixed := &rsa.PSSOptions{SaltLength: 20, Hash: crypto.SHA256}

	// Each mode round-trips against itself.
	sAuto, _ := rsa.SignPSS(rand.Reader, key, crypto.SHA256, h[:], auto)
	sEq, _ := rsa.SignPSS(rand.Reader, key, crypto.SHA256, h[:], eq)
	sFx, _ := rsa.SignPSS(rand.Reader, key, crypto.SHA256, h[:], fixed)
	sNil, _ := rsa.SignPSS(rand.Reader, key, crypto.SHA256, h[:], nil)
	fmt.Println("len:", len(sAuto), len(sEq), len(sFx), len(sNil))
	fmt.Println("auto:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], sAuto, auto) == nil)
	fmt.Println("eq:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], sEq, eq) == nil)
	fmt.Println("fixed:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], sFx, fixed) == nil)
	fmt.Println("nil==auto:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], sNil, nil) == nil)

	// Cross-mode: Auto verification recovers the salt length, so it accepts every signature;
	// a fixed/EqualsHash verifier rejects a signature whose salt length differs.
	fmt.Println("eq->auto:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], sEq, auto) == nil)
	fmt.Println("fixed->auto:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], sFx, auto) == nil)
	fmt.Println("auto->eq:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], sAuto, eq) == nil)
	fmt.Println("eq->fixed:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], sEq, fixed) == nil)

	// Tampered signature and wrong digest both fail.
	bad := make([]byte, len(sAuto))
	copy(bad, sAuto)
	bad[10] ^= 0x80
	fmt.Println("tampered:", rsa.VerifyPSS(pub, crypto.SHA256, h[:], bad, auto) == nil)
	other := sha256.Sum256([]byte("a different message"))
	fmt.Println("wrong-digest:", rsa.VerifyPSS(pub, crypto.SHA256, other[:], sAuto, auto) == nil)

	// SHA-384 and SHA-512 hashes work through the same code path.
	h3 := sha512.Sum384(msg)
	o3 := &rsa.PSSOptions{SaltLength: rsa.PSSSaltLengthEqualsHash, Hash: crypto.SHA384}
	s3, _ := rsa.SignPSS(rand.Reader, key, crypto.SHA384, h3[:], o3)
	fmt.Println("sha384:", rsa.VerifyPSS(pub, crypto.SHA384, h3[:], s3, o3) == nil)
	h5 := sha512.Sum512(msg)
	o5 := &rsa.PSSOptions{SaltLength: rsa.PSSSaltLengthAuto, Hash: crypto.SHA512}
	s5, _ := rsa.SignPSS(rand.Reader, key, crypto.SHA512, h5[:], o5)
	fmt.Println("sha512:", rsa.VerifyPSS(pub, crypto.SHA512, h5[:], s5, o5) == nil)
}
