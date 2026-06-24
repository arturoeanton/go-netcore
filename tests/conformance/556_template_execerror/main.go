package main

import (
	"bytes"
	"errors"
	"fmt"
	"text/template"
)

func main() {
	// ExecError value-type error.
	base := errors.New("nil pointer evaluating .Missing")
	ee := template.ExecError{Name: "page", Err: base}
	fmt.Println("error:", ee.Error())
	fmt.Println("name:", ee.Name)
	fmt.Println("is base:", errors.Is(ee, base))
	fmt.Println("unwrap:", errors.Unwrap(ee).Error())

	// Template.Clone: clone a parsed template, both render identically.
	t := template.Must(template.New("greet").Parse("Hello {{.Name}}, you are {{.Age}}!"))
	c, err := t.Clone()
	fmt.Println("clone err:", err, "name:", c.Name())
	data := map[string]any{"Name": "Ada", "Age": 30}
	var b1, b2 bytes.Buffer
	t.Execute(&b1, data)
	c.Execute(&b2, data)
	fmt.Printf("orig=%q clone=%q same=%v\n", b1.String(), b2.String(), b1.String() == b2.String())
}
