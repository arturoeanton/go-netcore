package main

import (
	"fmt"
	"reflect"
)

// Descriptor-based reflect: precise kind (sized ints), type strings, struct fields
// with type and tag, and element/key types of empty composites — all from the
// compile-time type, not a sample value.
type Addr struct {
	City string `json:"city"`
	Zip  int    `json:"zip"`
}
type User struct {
	Name  string `json:"name" validate:"required"`
	Age   uint8  `json:"age"`
	Email string `json:"email,omitempty"`
	Addr  Addr   `json:"addr"`
	Tags  []string
}

func main() {
	u := User{Name: "Ada", Age: 36, Addr: Addr{City: "X", Zip: 10}, Tags: []string{"p"}}
	t := reflect.TypeOf(u)
	fmt.Println(t.Kind(), t.Name(), t.NumField())
	for i := 0; i < t.NumField(); i++ {
		f := t.Field(i)
		fmt.Printf("%s %s %s %q\n", f.Name, f.Type, f.Type.Kind(), f.Tag.Get("json"))
	}
	v := reflect.ValueOf(u)
	fmt.Println(v.Kind(), v.Field(0).String(), v.Field(1).Uint())

	var x8 uint8
	var x32 int32
	fmt.Println(reflect.TypeOf(x8).Kind(), reflect.TypeOf(x32).Kind())
	fmt.Println(reflect.TypeOf([]string{}).Elem().Kind())
	mt := reflect.TypeOf(map[string]int{})
	fmt.Println(mt.Kind(), mt.Key().Kind(), mt.Elem().Kind())
	fmt.Println(reflect.TypeOf(&u).Elem().Kind())
}
