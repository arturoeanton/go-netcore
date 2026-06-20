package main

import (
	"encoding/json"
	"fmt"
)

type Addr struct {
	City string `json:"city"`
	Zip  string `json:"zip"`
}
type Person struct {
	Name string         `json:"name"`
	Age  int            `json:"age"`
	Tags []string       `json:"tags"`
	Addr Addr           `json:"addr"`
	Meta map[string]int `json:"meta"`
}

func main() {
	p := Person{
		Name: "amy",
		Age:  30,
		Tags: []string{"x", "y"},
		Addr: Addr{City: "NYC", Zip: "10001"},
		Meta: map[string]int{"a": 1},
	}
	b, _ := json.MarshalIndent(p, "", "  ")
	fmt.Println(string(b))

	// with a prefix
	c, _ := json.MarshalIndent([]int{1, 2, 3}, ">", "  ")
	fmt.Println(string(c))

	// empty containers stay compact
	d, _ := json.MarshalIndent(struct {
		A []int          `json:"a"`
		B map[string]int `json:"b"`
	}{[]int{}, map[string]int{}}, "", "\t")
	fmt.Println(string(d))
}
