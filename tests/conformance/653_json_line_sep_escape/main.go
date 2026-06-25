package main

import (
	"bytes"
	"encoding/json"
	"fmt"
)

// encoding/json always escapes U+2028 (line separator) and U+2029 (paragraph separator) to
//   /   — they are valid JSON but break JavaScript — regardless of SetEscapeHTML.
// Control chars below 0x20 escape to \u00XX (no \b/\f short forms), with \n\t\r short forms.
func main() {
	s := "line sep para\twith\bctrl\f"
	b, _ := json.Marshal(s)
	fmt.Println(string(b))

	m := map[string]string{"k": s}
	b2, _ := json.Marshal(m)
	fmt.Println(string(b2))

	type T struct {
		A string `json:"a"`
	}
	b3, _ := json.Marshal(T{s})
	fmt.Println(string(b3))

	b4, _ := json.Marshal([]string{s, "<x>&y"})
	fmt.Println(string(b4))

	var buf bytes.Buffer
	enc := json.NewEncoder(&buf)
	enc.SetEscapeHTML(false)
	enc.Encode(s)
	fmt.Print(buf.String())
}
