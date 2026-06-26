package main

import (
	"encoding/json"
	"errors"
	"fmt"
)

// Malformed JSON makes json.Unmarshal return a *json.SyntaxError with a positive
// byte Offset, recoverable with errors.As. Previously the shim returned a plain
// error, so errors.As(*json.SyntaxError) failed.
func main() {
	for _, s := range []string{
		`{bad`, `[1,2,`, `"unterminated`, `{"a":}`, `tru`, `123abc`,
		`{"k" "v"}`, `[1 2 3]`, `{,}`, ``,
	} {
		var v interface{}
		err := json.Unmarshal([]byte(s), &v)
		var se *json.SyntaxError
		ok := errors.As(err, &se)
		fmt.Printf("%-16q err=%t as=%t off>0=%t\n", s, err != nil, ok, ok && se.Offset > 0)
	}

	// Valid JSON: no error, no SyntaxError.
	var v interface{}
	err := json.Unmarshal([]byte(`{"ok":[1,2,3],"n":3.14}`), &v)
	var se *json.SyntaxError
	fmt.Println(err == nil, errors.As(err, &se))

	// SyntaxError also surfaces through a Decoder.
	var v2 interface{}
	derr := json.Unmarshal([]byte(`{"x": tru}`), &v2)
	var se2 *json.SyntaxError
	fmt.Println(errors.As(derr, &se2))
}
