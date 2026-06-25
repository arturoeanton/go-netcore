package main

import (
	"encoding/json"
	"fmt"
	"strings"
)

func main() {
	// UseNumber keeps numbers as json.Number (preserving large-int precision).
	dec := json.NewDecoder(strings.NewReader(`{"big":12345678901234567,"f":3.14,"nested":{"x":99},"arr":[1,2]}`))
	dec.UseNumber()
	var v map[string]any
	if err := dec.Decode(&v); err != nil {
		panic(err)
	}
	fmt.Printf("big=%T:%v f=%T:%v\n", v["big"], v["big"], v["f"], v["f"])

	n := v["big"].(json.Number)
	i, _ := n.Int64()
	fl, _ := n.Float64()
	fmt.Printf("int64=%d float64=%g str=%s\n", i, fl, n.String())

	// Nested object numbers are json.Number too.
	nested := v["nested"].(map[string]any)
	fmt.Printf("nested.x=%T:%v\n", nested["x"], nested["x"])

	// Without UseNumber, numbers decode as float64.
	dec2 := json.NewDecoder(strings.NewReader(`{"y":42}`))
	var w map[string]any
	dec2.Decode(&w)
	fmt.Printf("y=%T:%v\n", w["y"], w["y"])
}
