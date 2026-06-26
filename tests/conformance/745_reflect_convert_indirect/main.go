package main

import (
	"fmt"
	"reflect"
)

type Animal struct {
	Name string
	Legs int
}

// reflect.Value.Convert carries the target type (int->int64, float->int truncating, and
// int->string as Go's string(rune) — the code point's UTF-8, U+FFFD when out of range).
// reflect.Indirect keeps the pointed-at element's package-qualified type.
func main() {
	fmt.Println(reflect.ValueOf(65).Convert(reflect.TypeOf(int64(0))).Type())
	fmt.Println(reflect.ValueOf(3.9).Convert(reflect.TypeOf(0)).Interface())
	fmt.Println(reflect.ValueOf(int64(300)).Convert(reflect.TypeOf(uint32(0))).Type())
	for _, n := range []int{65, 0x4e16, 0x1F600, -1, 0xD800, 0x110000} {
		fmt.Printf("%q ", reflect.ValueOf(n).Convert(reflect.TypeOf("")).Interface())
	}
	fmt.Println()

	pa := &Animal{"Dog", 4}
	fmt.Println(reflect.Indirect(reflect.ValueOf(pa)).Type())
	fmt.Println(reflect.Indirect(reflect.ValueOf(42)).Type())
	fmt.Println(reflect.Indirect(reflect.ValueOf(pa)).Field(0).Interface())
	fmt.Println(reflect.Indirect(reflect.ValueOf(pa)).Type() == reflect.TypeOf(Animal{}))

	// Zero of the scalar/composite types that are supported.
	fmt.Printf("%v %v %q %v %v %v\n",
		reflect.Zero(reflect.TypeOf(0)).Interface(),
		reflect.Zero(reflect.TypeOf(3.14)).Interface(),
		reflect.Zero(reflect.TypeOf("")).Interface(),
		reflect.Zero(reflect.TypeOf(true)).Interface(),
		reflect.Zero(reflect.TypeOf(int64(0))).Interface(),
		reflect.Zero(reflect.TypeOf(uint8(0))).Interface())
}
