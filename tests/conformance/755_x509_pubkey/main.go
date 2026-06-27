package main

import (
	"crypto"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/rsa"
	"crypto/sha256"
	"crypto/x509"
	"encoding/pem"
	"fmt"
)

// x509 public-key DER: MarshalPKIXPublicKey / ParsePKIXPublicKey (SubjectPublicKeyInfo) and
// MarshalPKCS1PublicKey / ParsePKCS1PublicKey, backed by .NET's Export/Import for RSA and
// ECDSA — the round-trip that JWT RS*/ES* verifiers rely on. Keys are random, so the DER bytes
// are not reproducible, but every round-trip outcome (and a signature verified through a parsed
// key) is deterministic and byte-exact with go run.
func main() {
	rk, _ := rsa.GenerateKey(rand.Reader, 2048)
	msg := []byte("verify me")
	h := sha256.Sum256(msg)
	sig, _ := rsa.SignPKCS1v15(rand.Reader, rk, crypto.SHA256, h[:])

	der, err := x509.MarshalPKIXPublicKey(&rk.PublicKey)
	fmt.Println("rsa marshal ok:", err == nil, len(der) > 0)
	pemBytes := pem.EncodeToMemory(&pem.Block{Type: "PUBLIC KEY", Bytes: der})
	blk, _ := pem.Decode(pemBytes)
	pub, err := x509.ParsePKIXPublicKey(blk.Bytes)
	fmt.Println("rsa parse err:", err)
	rp := pub.(*rsa.PublicKey)
	fmt.Println("rsa N match:", rp.N.Cmp(rk.PublicKey.N) == 0, "E:", rp.E)
	fmt.Println("rsa verify via parsed key:", rsa.VerifyPKCS1v15(rp, crypto.SHA256, h[:], sig) == nil)

	// PKCS#1 RSAPublicKey round-trip.
	pk1 := x509.MarshalPKCS1PublicKey(&rk.PublicKey)
	rp1, err := x509.ParsePKCS1PublicKey(pk1)
	fmt.Println("pkcs1 parse err:", err, "E:", rp1.E, "N bits:", rp1.N.BitLen())

	// ECDSA across all three .NET-supported curves: marshal, parse, verify.
	for _, c := range []elliptic.Curve{elliptic.P256(), elliptic.P384(), elliptic.P521()} {
		ek, _ := ecdsa.GenerateKey(c, rand.Reader)
		r, s, _ := ecdsa.Sign(rand.Reader, ek, h[:])
		eder, _ := x509.MarshalPKIXPublicKey(&ek.PublicKey)
		epub, perr := x509.ParsePKIXPublicKey(eder)
		ep := epub.(*ecdsa.PublicKey)
		fmt.Println(ep.Curve.Params().Name, "parse err:", perr,
			"X match:", ep.X.Cmp(ek.X) == 0, "verify:", ecdsa.Verify(ep, h[:], r, s))
	}

	// An unsupported key type reports an honest error, not a bogus key.
	_, merr := x509.MarshalPKIXPublicKey("not a key")
	fmt.Println("bad type err:", merr != nil)
}
