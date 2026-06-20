package main

import (
	"fmt"
	"reflect"
)

type Config struct {
	Name  string
	Count int
	Ratio float64
	On    bool
}

func main() {
	x := 0
	v := reflect.ValueOf(&x).Elem()
	fmt.Println(v.CanSet())
	v.SetInt(42)
	fmt.Println(x)

	s := "hi"
	sv := reflect.ValueOf(&s).Elem()
	sv.SetString("changed")
	fmt.Println(s)

	c := Config{}
	rv := reflect.ValueOf(&c).Elem()
	rv.Field(0).SetString("server")
	rv.Field(1).SetInt(7)
	rv.Field(2).SetFloat(1.5)
	rv.Field(3).SetBool(true)
	fmt.Printf("%+v\n", c)

	// reflect.New
	nv := reflect.New(reflect.TypeOf(0))
	nv.Elem().SetInt(99)
	fmt.Println(nv.Elem().Int())

	// not settable
	y := 5
	yv := reflect.ValueOf(y)
	fmt.Println(yv.CanSet())
}
