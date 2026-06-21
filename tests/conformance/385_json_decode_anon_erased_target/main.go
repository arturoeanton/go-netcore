// json.Decoder.Decode into an anonymous struct, including through an interface{}
// parameter that erases the static target type (the shape gin's BindJSON uses): the
// value must decode into the concrete struct, not a generic map.
package main

import (
	"encoding/json"
	"fmt"
	"io"
	"strings"
)

func decodeInto(r io.Reader, obj any) error { return json.NewDecoder(r).Decode(obj) }

func main() {
	// direct concrete target
	var a struct {
		Text string `json:"text"`
		N    int    `json:"n"`
	}
	json.NewDecoder(strings.NewReader(`{"text":"hi","n":5}`)).Decode(&a)
	fmt.Printf("%s/%d\n", a.Text, a.N)

	// erased target (passed as any through a helper)
	var b struct {
		Text string `json:"text"`
		Ok   bool   `json:"ok"`
	}
	_ = decodeInto(strings.NewReader(`{"text":"world","ok":true}`), &b)
	fmt.Printf("%s/%t\n", b.Text, b.Ok)

	// json.Unmarshal into an anonymous struct
	var c struct {
		Vals []int `json:"vals"`
	}
	json.Unmarshal([]byte(`{"vals":[1,2,3]}`), &c)
	fmt.Println(c.Vals)
}
