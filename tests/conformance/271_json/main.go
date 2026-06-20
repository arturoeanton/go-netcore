package main

import "encoding/json"

type Address struct {
	City string `json:"city"`
	Zip  string `json:"zip,omitempty"`
}

type Person struct {
	Name  string   `json:"name"`
	Age   int      `json:"age"`
	Email string   `json:"email,omitempty"`
	Tags  []string `json:"tags"`
	Addr  Address  `json:"address"`
	Skip  string   `json:"-"`
}

func main() {
	p := Person{Name: "Ada", Age: 36, Tags: []string{"a", "b"}, Addr: Address{City: "London"}, Skip: "x"}
	b, _ := json.Marshal(p)
	println(string(b))
	m := map[string]int{"z": 1, "a": 2, "m": 3}
	mb, _ := json.Marshal(m)
	println(string(mb))
	nb, _ := json.Marshal([]int{1, 2, 3})
	println(string(nb))
	bb, _ := json.Marshal(true)
	println(string(bb))
}
