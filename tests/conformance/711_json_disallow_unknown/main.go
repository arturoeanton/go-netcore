package main

import (
	"encoding/json"
	"fmt"
	"strings"
)

type Base struct {
	ID int `json:"id"`
}
type Nested struct {
	A int `json:"a"`
}
type Doc struct {
	Base
	Name  string `json:"name"`
	Inner Nested `json:"inner"`
	Skip  string `json:"-"`
}

// json.Decoder.DisallowUnknownFields: an unknown key makes Decode return
// `json: unknown field "<name>"` while still populating the known fields. The
// check must respect promoted (embedded) fields, recurse into nested named
// structs, match field names case-insensitively, and treat a json:"-" field as
// unknown.
func dec(s string) error {
	d := json.NewDecoder(strings.NewReader(s))
	d.DisallowUnknownFields()
	var doc Doc
	return d.Decode(&doc)
}

func main() {
	fmt.Println(dec(`{"id":1,"name":"x","inner":{"a":2}}`)) // nil (all known incl promoted)
	fmt.Println(dec(`{"id":1,"name":"x","zzz":9}`))         // unknown field "zzz"
	fmt.Println(dec(`{"id":1,"inner":{"a":2,"bad":3}}`))    // nested unknown "bad"
	fmt.Println(dec(`{"unknown1":1,"unknown2":2}`))         // first: "unknown1"
	fmt.Println(dec(`{"skip":"v"}`))                        // json:"-" => unknown "skip"
	fmt.Println(dec(`{"ID":1,"NAME":"y"}`))                 // caseless match => nil
	fmt.Println(dec(`{}`))                                  // empty => nil

	// Known fields are still populated alongside the error.
	d := json.NewDecoder(strings.NewReader(`{"id":7,"name":"keep","extra":1}`))
	d.DisallowUnknownFields()
	var doc Doc
	err := d.Decode(&doc)
	fmt.Println(err, doc.ID, doc.Name)

	// Without DisallowUnknownFields, an unknown key is ignored.
	var doc2 Doc
	fmt.Println(json.Unmarshal([]byte(`{"id":1,"whatever":5}`), &doc2), doc2.ID)

	// Plain map target: unknown-field check does not apply (no fields to be unknown).
	dm := json.NewDecoder(strings.NewReader(`{"anything":1,"goes":2}`))
	dm.DisallowUnknownFields()
	var m map[string]int
	fmt.Println(dm.Decode(&m), m["anything"], m["goes"])
}
