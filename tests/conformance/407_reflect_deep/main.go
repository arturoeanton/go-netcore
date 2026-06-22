package main

import (
	"fmt"
	"reflect"
)

func add(a, b int) int { return a + b }

type Calc struct{ base int }

func (c Calc) Add(x int) int         { return c.base + x }
func (c Calc) Label(s string) string { return fmt.Sprintf("%s=%d", s, c.base) }
func (c Calc) Pair(n int) (int, int) { return c.base, c.base + n }

func main() {
	// Value.Call on a plain function value.
	f := reflect.ValueOf(add)
	out := f.Call([]reflect.Value{reflect.ValueOf(3), reflect.ValueOf(4)})
	fmt.Println("Call add(3,4):", out[0].Int())

	// MakeFunc: build a callable from a []Value -> []Value implementation.
	ft := reflect.TypeOf(func(int) (int, string) { return 0, "" })
	mf := reflect.MakeFunc(ft, func(args []reflect.Value) []reflect.Value {
		n := args[0].Int()
		return []reflect.Value{reflect.ValueOf(int(n * n)), reflect.ValueOf(fmt.Sprintf("sq(%d)", n))}
	})
	g := mf.Interface().(func(int) (int, string))
	sq, s := g(9)
	fmt.Println("MakeFunc:", sq, s)
	c2 := mf.Call([]reflect.Value{reflect.ValueOf(4)})
	fmt.Println("MakeFunc via Call:", c2[0].Int(), c2[1].String())

	// Value.MethodByName(...).Call on a user type.
	v := reflect.ValueOf(Calc{base: 10})
	a := v.MethodByName("Add").Call([]reflect.Value{reflect.ValueOf(5)})
	fmt.Println("MethodByName Add:", a[0].Int())
	l := v.MethodByName("Label").Call([]reflect.Value{reflect.ValueOf("base")})
	fmt.Println("MethodByName Label:", l[0].String())
	p := v.MethodByName("Pair").Call([]reflect.Value{reflect.ValueOf(7)})
	fmt.Println("MethodByName Pair:", p[0].Int(), p[1].Int())
}
