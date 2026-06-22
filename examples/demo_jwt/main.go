// demo_jwt signs and verifies a JWT (HS256) on the CLR via github.com/golang-jwt/jwt/v5.
// Requires `go mod vendor`.
package main

import (
	"fmt"

	"github.com/golang-jwt/jwt/v5"
)

func main() {
	secret := []byte("goclr-secret")

	claims := jwt.MapClaims{"sub": "user-42", "role": "admin", "n": 7}
	tok := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	signed, err := tok.SignedString(secret)
	if err != nil {
		fmt.Println("sign err:", err)
		return
	}
	fmt.Println("signed len > 20:", len(signed) > 20)

	parsed, err := jwt.Parse(signed, func(t *jwt.Token) (any, error) { return secret, nil })
	if err != nil {
		fmt.Println("parse err:", err)
		return
	}
	fmt.Println("valid:", parsed.Valid)
	mc := parsed.Claims.(jwt.MapClaims)
	fmt.Println("sub:", mc["sub"], "role:", mc["role"])

	// Wrong key must fail verification.
	_, badErr := jwt.Parse(signed, func(t *jwt.Token) (any, error) { return []byte("wrong"), nil })
	fmt.Println("wrong-key rejected:", badErr != nil)
}
