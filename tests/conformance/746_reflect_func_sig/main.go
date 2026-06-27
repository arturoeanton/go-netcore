package main

import (
	"fmt"
	"reflect"
)

type Handler func(string, int) (bool, error)
type P struct{ X int }

// A func type's reflect signature: NumIn/NumOut and In(i)/Out(i) report the parameter and
// result types (and those reflect.Type values compare equal to the same types elsewhere).
func main() {
	ft := reflect.TypeOf(func(a int, b string, c float64) (int, error) { return 0, nil })
	fmt.Println(ft.Kind(), ft.NumIn(), ft.NumOut())
	for i := 0; i < ft.NumIn(); i++ {
		fmt.Print(ft.In(i), " ")
	}
	fmt.Println()
	for i := 0; i < ft.NumOut(); i++ {
		fmt.Print(ft.Out(i), " ")
	}
	fmt.Println()

	fmt.Println(reflect.TypeOf(func() {}).NumIn(), reflect.TypeOf(func() {}).NumOut())

	var h Handler
	ht := reflect.TypeOf(h)
	fmt.Println(ht.Kind(), ht.NumIn(), ht.NumOut(), ht.In(0), ht.In(1), ht.Out(0))

	fmt.Println(ft.In(0) == reflect.TypeOf(0))
	fmt.Println(ft.Out(1) == reflect.TypeOf((*error)(nil)).Elem())

	ft3 := reflect.TypeOf(func([]int, map[string]int, P) *P { return nil })
	fmt.Println(ft3.In(0), ft3.In(1), ft3.In(2), ft3.Out(0))

	// Calling still works alongside introspection.
	add := reflect.ValueOf(func(a, b int) int { return a + b })
	r := add.Call([]reflect.Value{reflect.ValueOf(20), reflect.ValueOf(22)})
	fmt.Println(r[0].Int())
}
