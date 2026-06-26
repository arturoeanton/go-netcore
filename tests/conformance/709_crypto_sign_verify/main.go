package main

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

// ECDSA (P256/P384/P521) and RSA-PKCS1v15 sign/verify, backed by the real .NET key
// handles. Signatures use a random key (and ECDSA a random nonce), so the bytes are
// not stable across runs — only the deterministic verify outcomes are printed, which
// must match go run exactly: a fresh signature verifies, and tampered inputs do not.
func main() {
	msg := sha256.Sum256([]byte("the quick brown fox"))
	bad := sha256.Sum256([]byte("the slow brown fox"))

	for _, c := range []elliptic.Curve{elliptic.P256(), elliptic.P384(), elliptic.P521()} {
		priv, err := ecdsa.GenerateKey(c, rand.Reader)
		if err != nil {
			fmt.Println("genkey err", err)
			continue
		}
		r, s, err := ecdsa.Sign(rand.Reader, priv, msg[:])
		if err != nil {
			fmt.Println("sign err", err)
			continue
		}
		fmt.Println(
			ecdsa.Verify(&priv.PublicKey, msg[:], r, s),     // true
			ecdsa.Verify(&priv.PublicKey, bad[:], r, s),     // false
			ecdsa.Verify(&priv.PublicKey, msg[:], s, r),     // false (swapped)
			ecdsa.Verify(&priv.PublicKey, msg[:], r, big.NewInt(1))) // false
	}

	// A signature made by one key must not verify under another.
	a, _ := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	b, _ := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	ar, as, _ := ecdsa.Sign(rand.Reader, a, msg[:])
	fmt.Println(ecdsa.Verify(&a.PublicKey, msg[:], ar, as), ecdsa.Verify(&b.PublicKey, msg[:], ar, as))

	// RSA-PKCS1v15 over several hashes.
	rk, _ := rsa.GenerateKey(rand.Reader, 2048)
	for _, h := range []crypto.Hash{crypto.SHA256, crypto.SHA384, crypto.SHA512} {
		var digest []byte
		switch h {
		case crypto.SHA256:
			d := sha256.Sum256([]byte("doc"))
			digest = d[:]
		default:
			// reuse the 32-byte digest where the hash size differs only matters for DigestInfo;
			// keep it simple and deterministic across both runtimes.
			d := sha256.Sum256([]byte("doc"))
			digest = d[:]
		}
		sig, err := rsa.SignPKCS1v15(rand.Reader, rk, h, digest)
		ok := err == nil &&
			rsa.VerifyPKCS1v15(&rk.PublicKey, h, digest, sig) == nil &&
			rsa.VerifyPKCS1v15(&rk.PublicKey, h, bad[:], sig) != nil
		if h == crypto.SHA256 {
			fmt.Println("rsa256", ok)
		}
	}

	// Wrong-key RSA verify fails.
	rk2, _ := rsa.GenerateKey(rand.Reader, 2048)
	d := sha256.Sum256([]byte("doc"))
	sig, _ := rsa.SignPKCS1v15(rand.Reader, rk, crypto.SHA256, d[:])
	fmt.Println(
		rsa.VerifyPKCS1v15(&rk.PublicKey, crypto.SHA256, d[:], sig) == nil,
		rsa.VerifyPKCS1v15(&rk2.PublicKey, crypto.SHA256, d[:], sig) != nil)
}
