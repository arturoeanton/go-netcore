package main

import "fmt"

// Runtime panic messages match Go's text exactly, and a failed concrete type
// assertion panics ("interface conversion: ...") instead of crashing.
func try(name string, f func()) {
	defer func() {
		if r := recover(); r != nil {
			fmt.Printf("%s: %v\n", name, r)
		}
	}()
	f()
}

func main() {
	try("index", func() { s := []int{1, 2}; _ = s[5] })
	try("slice", func() { s := []int{1, 2}; _ = s[1:5] })
	try("strslice", func() { s := "ab"; _ = s[1:9] })
	try("nilmap", func() { var m map[string]int; m["x"] = 1 })
	try("div0", func() { a, b := 1, 0; _ = a / b })
	try("nilderef", func() { var p *int; _ = *p })
	try("assert", func() { var i interface{} = "s"; _ = i.(int) })
	try("assert2", func() { var i interface{} = 3; _ = i.(string) })
	try("custom", func() { panic("boom") })
	try("err", func() { panic(fmt.Errorf("e%d", 1)) })
}
