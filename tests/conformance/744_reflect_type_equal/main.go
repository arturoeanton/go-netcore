package main

import (
	"fmt"
	"reflect"
)

type MyInt int

// reflect.Type is comparable with ==: two reflect.Type values are equal iff they denote
// the same type, regardless of which call produced them (TypeOf / TypeFor / Elem / Key /
// Field.Type / Value.Type), and a reflect.Type works as a map key.
func main() {
	fmt.Println(reflect.TypeOf(1) == reflect.TypeOf(2))
	fmt.Println(reflect.TypeOf(1) == reflect.TypeOf("s"))
	fmt.Println(reflect.TypeOf(1) == reflect.TypeOf(int64(1)))
	fmt.Println(reflect.TypeOf(MyInt(1)) == reflect.TypeOf(1))
	fmt.Println(reflect.TypeOf(MyInt(1)) == reflect.TypeOf(MyInt(2)))
	fmt.Println(reflect.TypeOf([]int{}).Elem() == reflect.TypeOf(0))
	fmt.Println(reflect.TypeOf([5]string{}).Elem() == reflect.TypeOf(""))
	fmt.Println(reflect.ValueOf(3.14).Type() == reflect.TypeOf(1.0))
	fmt.Println(reflect.TypeFor[int]() == reflect.TypeOf(0))
	fmt.Println(reflect.TypeFor[string]() == reflect.TypeOf(""))

	type P struct {
		X int
		S string
	}
	t := reflect.TypeOf(P{})
	fmt.Println(t.Field(0).Type == reflect.TypeOf(0))
	fmt.Println(t.Field(1).Type == reflect.TypeOf(""))
	fmt.Println(t == reflect.TypeOf(P{}))

	mt := reflect.TypeOf(map[string]int{})
	fmt.Println(mt.Key() == reflect.TypeOf(""))
	fmt.Println(mt.Elem() == reflect.TypeOf(0))

	seen := map[reflect.Type]int{}
	seen[reflect.TypeOf(1)]++
	seen[reflect.TypeOf(2)]++
	seen[reflect.TypeOf("x")]++
	seen[reflect.TypeOf(3)]++
	fmt.Println(seen[reflect.TypeOf(0)], seen[reflect.TypeOf("")], len(seen))

	fmt.Println(reflect.TypeOf(1) != reflect.TypeOf("s"))
	// String() still works on the (interned) types.
	fmt.Println(reflect.TypeOf(1), reflect.TypeOf(MyInt(0)), reflect.TypeOf([]int{}), reflect.TypeOf(P{}))
}
