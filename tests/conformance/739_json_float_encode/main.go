package main

import (
	"encoding/json"
	"fmt"
)

// encoding/json formats a float as Go's strconv does: shortest 'f' for 1e-6 <= |f| < 1e21,
// else 'e' (with e-09 -> e-9), never an int conversion — so a large whole-number float keeps
// full precision (1e20 -> 100000000000000000000, not an int64 overflow). float32 fields use
// the 32-bit shortest. Also covers strconv.FormatFloat('f', -1, 64) shortest-fixed directly.
func main() {
	for _, f := range []float64{
		0, 1, -1, 5, 100, 1e6, 1e20, 1e21, 1e22, 1e-6, 1e-7, 9.999999e20,
		0.0001, 0.1, 3.14159, -42.5, 123456789.123456789, 1e-9, 1.5e-10, 6.022e23,
		-1e21, -1e-7, 2.5, 0.5, 1e100, 1e-100, 1234567890123456789.0, 0.000001, 0.0000001,
	} {
		b, _ := json.Marshal(f)
		fmt.Printf("%g -> %s\n", f, b)
	}
	for _, f := range []float32{1e20, 0.1, 1e-7, 3.14, 1e8, 0.5} {
		b, _ := json.Marshal(f)
		fmt.Printf("f32 %v -> %s\n", f, b)
	}
	// Floats inside a struct/map.
	type T struct {
		A float64 `json:"a"`
		B float32 `json:"b"`
		C uint32  `json:"c"`
		D int16   `json:"d"`
	}
	b, _ := json.Marshal(T{A: 1e20, B: 0.25, C: 4000000000, D: -300})
	fmt.Println(string(b))
}
