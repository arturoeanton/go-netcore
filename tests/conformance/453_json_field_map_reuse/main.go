package main

import (
	"encoding/json"
	"fmt"
)

// json.Unmarshal writes through a struct-field map pointer and reuses (merges
// into) a non-nil map target rather than replacing it — the behaviour golang-jwt
// relies on when it aliases one MapClaims through token.Claims and a local copy.
type Tok struct {
	Header map[string]any
}

func main() {
	// unmarshal into a struct-field map (&t.Header)
	t := &Tok{}
	json.Unmarshal([]byte(`{"alg":"HS256","typ":"JWT"}`), &t.Header)
	alg, ok := t.Header["alg"].(string)
	fmt.Println(alg, ok)

	// a non-nil map target is reused: decoded entries merge in (existing kept), and
	// a value aliasing the same map observes them.
	m := map[string]any{"keep": 1}
	var shared any = m
	mm := shared.(map[string]any)
	json.Unmarshal([]byte(`{"sub":"user-42","n":7}`), &mm)
	fmt.Println(m["keep"], m["sub"], m["n"])

	// json.Number string-receiver methods parse their text.
	var n json.Number = "3.14"
	f, _ := n.Float64()
	var k json.Number = "42"
	i, _ := k.Int64()
	fmt.Println(f, i, n.String())
}
