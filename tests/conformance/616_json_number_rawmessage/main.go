package main

import (
	"encoding/json"
	"fmt"
)

// encoding/json's special named types: json.Number keeps the raw numeric literal
// (marshals unquoted, parses from a JSON number), and json.RawMessage captures /
// emits a value's raw JSON bytes verbatim. Both as struct fields and top-level.
type Doc struct {
	ID    json.Number     `json:"id"`
	Price json.Number     `json:"price"`
	Raw   json.RawMessage `json:"raw"`
	Body  json.RawMessage `json:"body"`
}

func main() {
	// Unmarshal: Number from a JSON number, RawMessage captures the value bytes.
	var d Doc
	err := json.Unmarshal([]byte(`{"id":123,"price":9.99,"raw":{"a":[1,2,3]},"body":"hi"}`), &d)
	fmt.Println(d.ID, d.Price, string(d.Raw), string(d.Body), err)

	// json.Number numeric accessors
	n, _ := d.ID.Int64()
	f, _ := d.Price.Float64()
	fmt.Println(n, f, d.ID.String())

	// Marshal: Number unquoted, RawMessage verbatim — round-trips.
	b, _ := json.Marshal(d)
	fmt.Println(string(b))

	// MarshalIndent keeps the same value encoding
	bi, _ := json.MarshalIndent(Doc{ID: "7", Price: "1.5", Raw: json.RawMessage(`[true]`), Body: json.RawMessage(`null`)}, "", "  ")
	fmt.Println(string(bi))

	// Top-level Marshal of the bare named types
	o1, _ := json.Marshal(json.Number("42"))
	o2, _ := json.Marshal(json.RawMessage(`{"x":[1,2]}`))
	fmt.Println(string(o1), string(o2))

	// Top-level Unmarshal into a RawMessage and a Number
	var rm json.RawMessage
	json.Unmarshal([]byte(`[1,2,{"k":"v"}]`), &rm)
	fmt.Println(string(rm))
	var num json.Number
	json.Unmarshal([]byte(`-0.5e3`), &num)
	fmt.Println(num)

	// omitempty: empty Number/RawMessage are omitted
	type Opt struct {
		N json.Number     `json:"n,omitempty"`
		R json.RawMessage `json:"r,omitempty"`
	}
	be, _ := json.Marshal(Opt{})
	bf, _ := json.Marshal(Opt{N: "5", R: json.RawMessage(`{"ok":true}`)})
	fmt.Println(string(be), string(bf))

	// Unmarshal into a slice/map of the named types still reads correctly
	type Coll struct {
		Nums []json.Number              `json:"nums"`
		M    map[string]json.RawMessage `json:"m"`
	}
	var c Coll
	json.Unmarshal([]byte(`{"nums":[1,2.5,-3],"m":{"a":{"k":1},"b":[7]}}`), &c)
	fmt.Println(c.Nums, string(c.M["a"]), string(c.M["b"]))
}
