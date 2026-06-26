package main

import (
	"encoding/json"
	"errors"
	"fmt"
)

// A user type implementing json.Marshaler (MarshalJSON) or encoding.TextMarshaler
// (MarshalText) controls its own JSON encoding, exactly as Go's encoder does — at the
// top level, as a struct field, as a slice/array element, as a map value, and (for a
// TextMarshaler) as a map key. Previously goclr ignored these methods and emitted the
// underlying representation, because the bare runtime value erased the named identity.
type Temp float64

func (t Temp) MarshalJSON() ([]byte, error) {
	return []byte(fmt.Sprintf(`{"celsius":%.1f}`, float64(t))), nil
}

type Color int

const (
	Red Color = iota
	Green
	Blue
)

func (c Color) MarshalText() ([]byte, error) {
	return []byte([]string{"red", "green", "blue"}[c]), nil
}

type Money struct{ Cents int }

func (m *Money) MarshalJSON() ([]byte, error) {
	return []byte(fmt.Sprintf(`"$%d.%02d"`, m.Cents/100, m.Cents%100)), nil
}

type Bad struct{}

func (b Bad) MarshalJSON() ([]byte, error) { return nil, errors.New("nope") }

type Report struct {
	Primary Temp            `json:"primary"`
	Temps   []Temp          `json:"temps"`
	Lookup  map[string]Temp `json:"lookup"`
	Price   *Money          `json:"price"`
}

func p(v any) {
	b, err := json.Marshal(v)
	fmt.Println(string(b), err)
}

func main() {
	// top level: Marshaler value, pointer-receiver Marshaler, TextMarshaler value
	p(Temp(25.5))
	p(&Money{12345})
	p(Green)

	// slices / maps of Marshaler + TextMarshaler element types
	p([]Temp{1.0, 2.0})
	p(map[string]Temp{"a": 3.5})
	p([]Color{Red, Blue})

	// TextMarshaler as a map key (sorted by the marshaled key text)
	p(map[Color]int{Blue: 2, Red: 1})

	// struct fields: scalar, slice, map, and pointer Marshaler fields
	p(Report{Primary: 37, Temps: []Temp{0, 100}, Lookup: map[string]Temp{"k": 9}, Price: &Money{999}})

	// indentation re-indents the embedded Marshaler output
	b, _ := json.MarshalIndent(struct {
		T Temp `json:"t"`
	}{5.0}, "", "  ")
	fmt.Println(string(b))

	// an error from MarshalJSON propagates with Go's "for type T" wording
	_, err := json.Marshal(Bad{})
	fmt.Println(err)
}
