package main

import (
	"fmt"
	"reflect"
)

type Calc struct{ Base int }

func (c Calc) Add(x int) int  { return c.Base + x }
func (c Calc) Name() string   { return "calc" }
func (c *Calc) Reset()        { c.Base = 0 }

// reflect Call/MethodByName, MakeSlice/Append, MakeMap, Convert (float->int truncates toward
// zero, not rounds), and Value.NumMethod (== Type.NumMethod; pointer sees pointer-receiver
// methods too).
func main() {
	c := Calc{10}
	v := reflect.ValueOf(c)
	res := v.MethodByName("Add").Call([]reflect.Value{reflect.ValueOf(5)})
	fmt.Println(res[0].Int())

	fmt.Println(v.NumMethod(), v.Type().NumMethod(), reflect.ValueOf(&c).NumMethod())
	t := v.Type()
	for i := 0; i < t.NumMethod(); i++ {
		fmt.Print(t.Method(i).Name, " ")
	}
	fmt.Println()

	sl := reflect.MakeSlice(reflect.SliceOf(reflect.TypeOf(0)), 0, 0)
	sl = reflect.Append(sl, reflect.ValueOf(1), reflect.ValueOf(2))
	fmt.Println(sl.Interface())

	mp := reflect.MakeMap(reflect.MapOf(reflect.TypeOf(""), reflect.TypeOf(0)))
	mp.SetMapIndex(reflect.ValueOf("k"), reflect.ValueOf(42))
	fmt.Println(mp.MapIndex(reflect.ValueOf("k")).Int())

	fmt.Println(reflect.ValueOf(3.7).Convert(reflect.TypeOf(0)).Int())
	fmt.Println(reflect.ValueOf(-3.7).Convert(reflect.TypeOf(0)).Int())
	fmt.Println(reflect.ValueOf(2.5).Convert(reflect.TypeOf(0)).Int())
	fmt.Println(reflect.ValueOf(7.9).Convert(reflect.TypeOf(uint(0))).Uint())
	fmt.Println(reflect.ValueOf(5).Convert(reflect.TypeOf(0.0)).Float())

	fmt.Println(reflect.ValueOf(5).NumMethod())
}
