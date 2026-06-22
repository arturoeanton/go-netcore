package main

import (
	"fmt"
	"reflect"
)

type Base struct {
	ID int `json:"id"`
}

type Rec struct {
	Base
	Title string `json:"title" xml:"t" validate:"required"`
	Count int    `json:"count,omitempty"`
	priv  string
}

func main() {
	t := reflect.TypeOf(Rec{})
	for i := 0; i < t.NumField(); i++ {
		f := t.Field(i)
		j, ok := f.Tag.Lookup("json")
		fmt.Printf("%-6s type=%s kind=%v anon=%v json=%q ok=%v validate=%q pkgpath=%q\n",
			f.Name, f.Type.Name(), f.Type.Kind(), f.Anonymous, j, ok, f.Tag.Get("validate"), f.PkgPath)
	}

	if f, ok := t.FieldByName("Title"); ok {
		fmt.Println("byname Title:", f.Name, f.Tag.Get("xml"), f.Type.Name())
	}
	if _, ok := t.FieldByName("Missing"); !ok {
		fmt.Println("byname Missing: not found")
	}
}
