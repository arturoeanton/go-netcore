package main

import (
	"encoding/json"
	"fmt"
)

type Base struct {
	ID      int    `json:"id"`
	Created string `json:"created,omitempty"`
}

type Audit struct {
	By string `json:"by"`
}

type Record struct {
	Base
	Audit
	Name string `json:"name"`
	Tags []string `json:"tags"`
}

type PtrDoc struct {
	*Base
	Title string `json:"title"`
}

type Nested struct {
	B Base `json:"base"` // explicit name -> stays nested, not promoted
	X int  `json:"x"`
}

func main() {
	var r Record
	must(json.Unmarshal([]byte(`{"id":5,"created":"t","by":"sys","name":"alice","tags":["a","b"]}`), &r))
	fmt.Printf("%+v\n", r)

	// Round-trips back to a flattened object.
	b, _ := json.Marshal(r)
	fmt.Println(string(b))

	// Embedded pointer is auto-allocated.
	var p PtrDoc
	must(json.Unmarshal([]byte(`{"id":7,"title":"hi"}`), &p))
	fmt.Println(p.Base.ID, p.Title)

	// An embedded field with an explicit json name stays nested.
	var n Nested
	must(json.Unmarshal([]byte(`{"base":{"id":9},"x":1}`), &n))
	fmt.Printf("%+v\n", n)
}

func must(err error) {
	if err != nil {
		panic(err)
	}
}
