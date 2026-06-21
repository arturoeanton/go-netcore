package main

import (
	"fmt"
	"reflect"
)

// reflect method set: NumMethod, Implements, AssignableTo, ConvertibleTo built from
// compile-time method-set descriptors.
type Stringer interface{ String() string }
type Animal interface {
	Sound() string
	Name() string
}
type Dog struct{ N string }

func (d Dog) Sound() string  { return "woof" }
func (d Dog) Name() string   { return d.N }
func (d Dog) String() string { return d.N }

func main() {
	dt := reflect.TypeOf(Dog{})
	st := reflect.TypeOf((*Stringer)(nil)).Elem()
	at := reflect.TypeOf((*Animal)(nil)).Elem()
	fmt.Println(dt.NumMethod(), st.NumMethod(), at.NumMethod())
	fmt.Println(dt.Implements(st), dt.Implements(at))
	fmt.Println(dt.AssignableTo(st), dt.AssignableTo(at))
	it, ft := reflect.TypeOf(0), reflect.TypeOf(0.0)
	fmt.Println(it.ConvertibleTo(ft), it.AssignableTo(reflect.TypeOf(5)), it.AssignableTo(ft))
}
