package main

import (
	"encoding/json"
	"fmt"
)

type T struct {
	A []int
	B []int
	M map[string]int
}
type Base struct {
	ID   int
	Kind string
}
type Outer struct {
	Base
	Name string
}
type In struct{ X int }
type P struct{ Ptr *In }

func main() {
	for _, v := range []T{{A: []int{1}, B: []int{2}}, {}, {A: []int{}}, {B: nil}} {
		b, _ := json.Marshal(v)
		fmt.Println(string(b))
	}
	o := Outer{Base: Base{ID: 1, Kind: "k"}, Name: "n"}
	b, _ := json.Marshal(o)
	fmt.Println(string(b))
	var ns []int
	nb, _ := json.Marshal(ns)
	fmt.Println(string(nb))
	var p P
	json.Unmarshal([]byte(`{"Ptr":{"X":9}}`), &p)
	fmt.Println(p.Ptr != nil, p.Ptr.X)
}
