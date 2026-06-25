package main

import (
	"encoding/xml"
	"fmt"
)

// A struct field tagged with a nested-element path (xml:"tags>tag") marshals into
// wrapper elements: <tags><tag>…</tag></tags>, with slice elements sharing one wrapper.
type Address struct {
	City string `xml:"city"`
	Zip  string `xml:"zip,attr"`
}
type Person struct {
	XMLName xml.Name `xml:"person"`
	Name    string   `xml:"name"`
	Age     int      `xml:"age"`
	Email   string   `xml:"email,omitempty"`
	Addr    Address  `xml:"address"`
	Tags    []string `xml:"tags>tag"`
	Deep    []string `xml:"a>b>c"`
}

type Cfg struct {
	Host string `xml:"server>host"`
	Port int    `xml:"server>port"`
}

func main() {
	p := Person{
		Name: "Alice", Age: 30,
		Addr: Address{City: "NYC", Zip: "10001"},
		Tags: []string{"a", "b"},
		Deep: []string{"x", "y"},
	}
	out, _ := xml.Marshal(p)
	fmt.Println(string(out))

	ind, _ := xml.MarshalIndent(p, "", "  ")
	fmt.Println(string(ind))

	// single (non-slice) values under a shared nested path
	c, _ := xml.Marshal(Cfg{Host: "localhost", Port: 8080})
	fmt.Println(string(c))

	// escaping inside a path element
	type Note struct {
		Lines []string `xml:"body>line"`
	}
	n, _ := xml.Marshal(Note{Lines: []string{"a < b", "c & d"}})
	fmt.Println(string(n))
}
