// crypto/x509 + ecdsa + elliptic + pkix: generate an EC key, create a self-signed
// certificate from a template, parse it back, and read its subject and SAN DNS names.
package main

import (
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/x509"
	"crypto/x509/pkix"
	"fmt"
	"math/big"
	"time"
)

func main() {
	key, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		fmt.Println("genkey:", err)
		return
	}
	tmpl := x509.Certificate{
		SerialNumber:          big.NewInt(42),
		Subject:               pkix.Name{CommonName: "goclr.example"},
		NotBefore:             time.Unix(0, 0).UTC(),
		NotAfter:              time.Unix(1<<31, 0).UTC(),
		DNSNames:              []string{"goclr.example", "www.goclr.example"},
		KeyUsage:              x509.KeyUsageDigitalSignature,
		BasicConstraintsValid: true,
	}
	der, err := x509.CreateCertificate(rand.Reader, &tmpl, &tmpl, &key.PublicKey, key)
	if err != nil {
		fmt.Println("create:", err)
		return
	}
	cert, err := x509.ParseCertificate(der)
	if err != nil {
		fmt.Println("parse:", err)
		return
	}
	fmt.Println("subject CN:", cert.Subject.CommonName)
	fmt.Println("dns names:", cert.DNSNames)

	keyDER, err := x509.MarshalECPrivateKey(key)
	fmt.Println("marshalled key:", err == nil && len(keyDER) > 0)
}
