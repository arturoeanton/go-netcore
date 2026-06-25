package main

import (
	"encoding/json"
	"fmt"
	"io"
	"strings"
)

// json.Decoder streaming: Token() tokenizes a value stream, and mixing Token() with
// Decode() (the idiomatic "consume '[', then Decode each element while More()") must
// advance over the separators — previously it desynced and looped forever.
func main() {
	// full Token() tokenization
	dec := json.NewDecoder(strings.NewReader(`{"name":"x","nums":[1,2.5,3],"ok":true,"nil":null}`))
	for {
		tok, err := dec.Token()
		if err == io.EOF {
			break
		}
		switch v := tok.(type) {
		case json.Delim:
			fmt.Printf("delim:%c ", v)
		case string:
			fmt.Printf("str:%q ", v)
		case float64:
			fmt.Printf("num:%v ", v)
		case bool:
			fmt.Printf("bool:%v ", v)
		case nil:
			fmt.Printf("null ")
		}
	}
	fmt.Println()

	// UseNumber keeps big integers exact
	dec2 := json.NewDecoder(strings.NewReader(`{"big":12345678901234567890}`))
	dec2.UseNumber()
	var m map[string]interface{}
	dec2.Decode(&m)
	fmt.Printf("%v %T\n", m["big"], m["big"])

	// Token('[') then Decode each element while More()  (the case that used to hang)
	dec3 := json.NewDecoder(strings.NewReader(`[{"a":1},{"a":2},{"a":3}]`))
	dec3.Token()
	sum := 0
	for dec3.More() {
		var obj map[string]int
		dec3.Decode(&obj)
		sum += obj["a"]
	}
	fmt.Println(sum)

	// stream of typed objects with More()
	dec4 := json.NewDecoder(strings.NewReader(`[{"n":"a","v":1},{"n":"b","v":2}]`))
	dec4.Token()
	type Item struct {
		N string `json:"n"`
		V int    `json:"v"`
	}
	for dec4.More() {
		var it Item
		dec4.Decode(&it)
		fmt.Printf("%s=%d ", it.N, it.V)
	}
	fmt.Println()

	// consecutive Decode of concatenated top-level values
	dec5 := json.NewDecoder(strings.NewReader(`{"a":1}{"b":2}[3,4]`))
	var o1, o2 map[string]int
	var arr []int
	dec5.Decode(&o1)
	dec5.Decode(&o2)
	dec5.Decode(&arr)
	fmt.Println(o1["a"], o2["b"], arr)
}
