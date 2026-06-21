package main

import (
	"fmt"
	"reflect"
)

// Reflect over a value passed as interface{}: the dynamic type is recovered from
// the value's identity (a struct's emitted type, a named type's typed-box id, or a
// boxed scalar), not from the static (interface) type at the call site.
type Point struct{ X, Y int }
type Celsius float64

func (c Celsius) String() string { return "C" }

func describe(v interface{}) {
	t := reflect.TypeOf(v)
	fmt.Println(t.Kind(), t.Name(), t.String())
}

func main() {
	describe(Point{1, 2})
	describe(Celsius(36.6))
	describe("hello")
	describe(42)
	describe(true)
	describe(3.14)
}
