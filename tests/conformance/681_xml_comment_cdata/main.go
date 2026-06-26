package main

import (
	"encoding/xml"
	"fmt"
)

// encoding/xml marshaling honors the ,comment (XML comment) and ,cdata (CDATA section)
// field tags, and closes any open nested-path wrapper before such a field. xml.Name also
// formats its string fields correctly under %v/%+v/%#v. (Previously ,comment emitted a
// <Comment> element, and xml.Name's fields printed as {}.)
type Person struct {
	XMLName xml.Name `xml:"person"`
	Name    string   `xml:"name"`
	Age     int      `xml:"age,attr"`
	Tags    []string `xml:"tags>tag"`
	Note    string   `xml:",comment"`
	Raw     string   `xml:",cdata"`
}

func main() {
	p := Person{Name: "Alice", Age: 30, Tags: []string{"a", "b"}, Note: "hello", Raw: "x < y & z"}
	b, _ := xml.Marshal(p)
	fmt.Println(string(b))
	bi, _ := xml.MarshalIndent(p, "", "  ")
	fmt.Println(string(bi))

	// empty ,comment is omitted
	p2 := Person{Name: "Bob", Age: 1, Raw: "ok"}
	b2, _ := xml.Marshal(p2)
	fmt.Println(string(b2))

	// xml.Name formatting
	n := xml.Name{Space: "ns", Local: "item"}
	fmt.Printf("%v %+v %#v\n", n, n, n)
	var z xml.Name
	fmt.Printf("%v %+v\n", z, z)
}
