package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"strings"
)

// json.Decoder reads a stream of concatenated values; json.Encoder.SetEscapeHTML(false)
// leaves <, >, & literal while json.Marshal keeps escaping them.
func main() {
	// concatenated objects
	dec := json.NewDecoder(strings.NewReader(`{"name":"a","price":1}{"name":"b","price":2}`))
	for dec.More() {
		var it struct {
			Name  string
			Price int
		}
		dec.Decode(&it)
		fmt.Printf("%s=%d ", it.Name, it.Price)
	}
	fmt.Println()

	// mixed value stream with whitespace
	dec2 := json.NewDecoder(strings.NewReader(`[1,2] {"a":1}  42  "hi"  true  null`))
	for dec2.More() {
		var v interface{}
		dec2.Decode(&v)
		fmt.Printf("%v|", v)
	}
	fmt.Println()

	// newline-delimited JSON + InputOffset
	dec3 := json.NewDecoder(strings.NewReader("{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n"))
	for dec3.More() {
		var rec struct{ ID int }
		dec3.Decode(&rec)
		fmt.Print(rec.ID, "@", dec3.InputOffset(), " ")
	}
	fmt.Println()

	// SetEscapeHTML toggle
	for _, esc := range []bool{true, false} {
		var buf bytes.Buffer
		e := json.NewEncoder(&buf)
		e.SetEscapeHTML(esc)
		e.Encode(map[string]string{"v": "a<b>c&d'e\"f"})
		fmt.Printf("esc=%v: %s", esc, buf.String())
	}

	// Marshal/MarshalIndent still escape HTML
	m, _ := json.Marshal("x<y>&z")
	fmt.Println(string(m))
	mi, _ := json.MarshalIndent(map[string]string{"h": "<a>"}, "", " ")
	fmt.Println(string(mi))
}
