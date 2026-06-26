package main

import (
	"encoding/json"
	"fmt"
)

// A truncated or empty JSON input reports Go's "unexpected end of JSON input". Type
// mismatches keep their Go-exact message, and valid input still decodes. (Other malformed
// inputs — invalid character mid-structure — surface the .NET reader text; see LIMITATIONS.)
func main() {
	var v interface{}
	fmt.Println(json.Unmarshal([]byte(`[1,2,`), &v))
	fmt.Println(json.Unmarshal([]byte(``), &v))
	fmt.Println(json.Unmarshal([]byte(`   `), &v))

	fmt.Println(json.Unmarshal([]byte(`[1,2,3]`), &v), v)
	fmt.Println(json.Unmarshal([]byte(`{"a":1}`), &v))

	var n int
	fmt.Println(json.Unmarshal([]byte(`"x"`), &n))

	fmt.Println(json.Valid([]byte(`{"a":1}`)), json.Valid([]byte(`{bad`)), json.Valid([]byte(`[1,2]`)))
}
