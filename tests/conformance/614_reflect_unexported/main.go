package main

import (
	"fmt"
	"reflect"
)

// reflect.Value from an unexported field is read-only: CanInterface/CanSet are false and
// Interface() panics, and the flag propagates to nested fields. Exported fields are
// unaffected; fmt still prints unexported fields (it uses internal access, not Interface).
type T struct {
	Public  string
	private int
	Nested  struct {
		Inner  string
		hidden bool
	}
}

func main() {
	t := T{Public: "p", private: 5}
	t.Nested.Inner = "ni"
	v := reflect.ValueOf(t)

	for i := 0; i < v.NumField(); i++ {
		f := v.Field(i)
		fmt.Printf("%s: iface=%v set=%v\n", reflect.TypeOf(t).Field(i).Name, f.CanInterface(), f.CanSet())
	}

	fmt.Println(v.Field(0).Interface())
	func() {
		defer func() { fmt.Println("panicked:", recover() != nil) }()
		_ = v.Field(1).Interface()
	}()

	pt := &T{}
	pv := reflect.ValueOf(pt).Elem()
	fmt.Println(pv.Field(0).CanSet(), pv.Field(1).CanSet())

	nested := v.FieldByName("Nested")
	fmt.Println(nested.CanInterface(), nested.Field(1).CanInterface())

	// fmt of the struct still includes the unexported field
	fmt.Printf("%v %+v\n", t, t)

	// exported field of a settable struct round-trips
	pv.Field(0).SetString("changed")
	fmt.Println(pt.Public)
}
