package main

import (
	"bytes"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/rsa"
	"fmt"
	"strings"
)

// Type assertions / type switches over opaque shim handles. These types all lower to a single
// System.Object at runtime, so a naive `isinst` would match every boxed value — a *rsa.PublicKey
// would wrongly satisfy x.(*ecdsa.PublicKey). Discrimination must go through the [GoShim]
// registry so each assertion only matches its own concrete shim class.
func classify(k interface{}) string {
	switch k.(type) {
	case *rsa.PublicKey:
		return "rsa"
	case *ecdsa.PublicKey:
		return "ecdsa"
	case *bytes.Buffer:
		return "buffer"
	case *strings.Builder:
		return "builder"
	default:
		return "other"
	}
}

func main() {
	rk, _ := rsa.GenerateKey(rand.Reader, 2048)
	ek, _ := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	var buf bytes.Buffer
	var sb strings.Builder

	// Type switch picks exactly one arm per shim type, and falls through for a non-shim.
	fmt.Println(classify(&rk.PublicKey))
	fmt.Println(classify(&ek.PublicKey))
	fmt.Println(classify(&buf))
	fmt.Println(classify(&sb))
	fmt.Println(classify(42))
	fmt.Println(classify("hi"))

	// comma-ok: each handle matches only its own type.
	var ri interface{} = &rk.PublicKey
	var ei interface{} = &ek.PublicKey
	if rp, ok := ri.(*rsa.PublicKey); ok {
		fmt.Println("rsa N bits:", rp.N.BitLen(), "E:", rp.E)
	}
	_, ok1 := ri.(*ecdsa.PublicKey)
	fmt.Println("rsa as ecdsa:", ok1)
	_, ok2 := ei.(*rsa.PublicKey)
	fmt.Println("ecdsa as rsa:", ok2)
	_, ok3 := ei.(*ecdsa.PublicKey)
	fmt.Println("ecdsa as ecdsa:", ok3)

	// A non-shim boxed value never matches a shim assertion.
	var ii interface{} = 7
	_, ok4 := ii.(*rsa.PublicKey)
	fmt.Println("int as rsa:", ok4)

	// bytes.Buffer is usable after a successful single-value assertion.
	var bi interface{} = &buf
	b := bi.(*bytes.Buffer)
	b.WriteString("hello")
	fmt.Println("buf:", b.String(), b.Len())

	// A wrong single-value shim assertion panics like Go.
	func() {
		defer func() {
			if r := recover(); r != nil {
				fmt.Println("recovered wrong assert")
			}
		}()
		_ = bi.(*strings.Builder)
		fmt.Println("NOT REACHED")
	}()
}
