package main

import (
	"fmt"
	"reflect"
	"strings"
)

type Inner struct{ Z int }
type Outer struct {
	Name  string
	Count int
	In    Inner
}

func main() {
	o := Outer{Name: "widget", Count: 7, In: Inner{Z: 99}}
	v := reflect.ValueOf(o)

	// FieldByIndexErr: top-level and nested.
	f0, err0 := v.FieldByIndexErr([]int{0})
	f2, err2 := v.FieldByIndexErr([]int{2, 0})
	fmt.Printf("name=%q err=%v  z=%d err=%v\n", f0.String(), err0, f2.Int(), err2)

	// FieldByNameFunc: case-insensitive match.
	cf := v.FieldByNameFunc(func(s string) bool { return strings.EqualFold(s, "count") })
	fmt.Println("count", cf.Int())
	nf := v.FieldByNameFunc(func(s string) bool { return s == "Name" })
	fmt.Printf("name=%q\n", nf.String())
	miss := v.FieldByNameFunc(func(s string) bool { return s == "Nope" })
	fmt.Println("missing valid:", miss.IsValid())
}
