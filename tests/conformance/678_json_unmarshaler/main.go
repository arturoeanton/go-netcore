package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"strings"
)

// A user type implementing json.Unmarshaler (UnmarshalJSON) or encoding.TextUnmarshaler
// (UnmarshalText) decodes through its own method, exactly as Go does — at the top level,
// as a struct field, a slice element, a map value, and a pointer. Previously goclr ignored
// these methods and produced the zero value / a type-mismatch error.
type Temp float64

func (t *Temp) UnmarshalJSON(b []byte) error {
	var s string
	if err := json.Unmarshal(b, &s); err != nil {
		return err
	}
	var f float64
	fmt.Sscanf(strings.TrimSuffix(s, "C"), "%g", &f)
	*t = Temp(f)
	return nil
}

type Color int

func (c *Color) UnmarshalText(b []byte) error {
	switch string(b) {
	case "red":
		*c = 0
	case "green":
		*c = 1
	case "blue":
		*c = 2
	default:
		return errors.New("bad color")
	}
	return nil
}

// A struct decoding from a compact string via the alias trick + field mutation.
type Point struct{ X, Y int }

func (p *Point) UnmarshalJSON(b []byte) error {
	parts := strings.Split(strings.Trim(string(b), `"`), ",")
	fmt.Sscanf(parts[0], "%d", &p.X)
	fmt.Sscanf(parts[1], "%d", &p.Y)
	return nil
}

type Doc struct {
	T      Temp    `json:"t"`
	C      Color   `json:"c"`
	Origin Point   `json:"origin"`
	Points []Point `json:"points"`
}

func main() {
	// struct combining a Marshaler-scalar field, a TextUnmarshaler field, and struct fields
	var d Doc
	err := json.Unmarshal([]byte(`{"t":"25.5C","c":"green","origin":"3,4","points":["1,2","5,6"]}`), &d)
	fmt.Println(d.T, d.C, d.Origin, d.Points, err)

	// top-level scalar / TextUnmarshaler value
	var t Temp
	json.Unmarshal([]byte(`"100C"`), &t)
	fmt.Println(t)

	// slice + map of an unmarshaler element type
	var ts []Temp
	json.Unmarshal([]byte(`["1C","2C"]`), &ts)
	fmt.Println(ts)
	var m map[string]Temp
	json.Unmarshal([]byte(`{"a":"3C"}`), &m)
	fmt.Println(m["a"])

	// top-level pointer to an unmarshaler (nil *T receives an allocated value)
	var p *Point
	json.Unmarshal([]byte(`"7,8"`), &p)
	fmt.Println(p.X, p.Y)

	// an error from the user method propagates as json.Unmarshal's error
	var c Color
	fmt.Println(json.Unmarshal([]byte(`"purple"`), &c))
}
