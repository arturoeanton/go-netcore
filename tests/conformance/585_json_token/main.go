package main

import (
	"encoding/json"
	"fmt"
	"strings"
)

func main() {
	// Decoder.Token streams delimiters (json.Delim) and scalar tokens.
	dec := json.NewDecoder(strings.NewReader(`{"name":"go","nums":[1,2,3],"ok":true,"x":null}`))
	for {
		t, err := dec.Token()
		if err != nil {
			break
		}
		fmt.Printf("%v(%T) ", t, t)
	}
	fmt.Println()

	// Type-switch over tokens, with More() to bound an array.
	dec2 := json.NewDecoder(strings.NewReader(`["a", 2, true, null, {"k":"v"}]`))
	dec2.Token() // consume '['
	for dec2.More() {
		t, _ := dec2.Token()
		switch x := t.(type) {
		case json.Delim:
			fmt.Printf("delim=%s ", x)
		case string:
			fmt.Printf("str=%q ", x)
		case float64:
			fmt.Printf("num=%g ", x)
		case bool:
			fmt.Printf("bool=%v ", x)
		case nil:
			fmt.Print("null ")
		}
		if d, ok := t.(json.Delim); ok && d == '{' {
			for dec2.More() {
				dec2.Token() // key
				dec2.Token() // value
			}
			dec2.Token() // '}'
		}
	}
	fmt.Println()
}
