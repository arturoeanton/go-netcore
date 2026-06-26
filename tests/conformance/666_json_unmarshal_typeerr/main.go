package main

import (
	"encoding/json"
	"fmt"
)

type Money int64
type Foo struct {
	A int `json:"a"`
}
type Bar struct {
	Inner Foo `json:"inner"`
}

// json.Unmarshal type mismatches report Go's exact error: "json: cannot unmarshal <jsonkind>
// into Go value of type <T>" at the top level, and "...into Go struct field <Struct>.<path> of
// type <T>" inside a struct (the innermost struct name + the JSON key chain).
func main() {
	var n int
	fmt.Println(json.Unmarshal([]byte(`"x"`), &n))
	var s string
	fmt.Println(json.Unmarshal([]byte(`42`), &s))
	var f float64
	fmt.Println(json.Unmarshal([]byte(`true`), &f))
	var b bool
	fmt.Println(json.Unmarshal([]byte(`"yes"`), &b))
	var i64 int64
	fmt.Println(json.Unmarshal([]byte(`[]`), &i64))
	var u uint
	fmt.Println(json.Unmarshal([]byte(`{}`), &u))
	var m Money
	fmt.Println(json.Unmarshal([]byte(`"x"`), &m))

	var foo Foo
	fmt.Println(json.Unmarshal([]byte(`{"a":"str"}`), &foo))
	var bar Bar
	fmt.Println(json.Unmarshal([]byte(`{"inner":{"a":[1]}}`), &bar))
	var an struct {
		N int `json:"n"`
	}
	fmt.Println(json.Unmarshal([]byte(`{"n":true}`), &an))

	// valid decodes still succeed
	var ok int
	fmt.Println(json.Unmarshal([]byte(`5`), &ok), ok)
	var okf float64
	json.Unmarshal([]byte(`5`), &okf)
	fmt.Println(okf)
}
