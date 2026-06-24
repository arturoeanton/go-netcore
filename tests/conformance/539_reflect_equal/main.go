package main

import (
	"fmt"
	"reflect"
)

type Widget struct{ N int }

func main() {
	// Value.Comparable across kinds.
	fmt.Println("int", reflect.ValueOf(42).Comparable())
	fmt.Println("str", reflect.ValueOf("hi").Comparable())
	fmt.Println("slice", reflect.ValueOf([]int{1}).Comparable())
	fmt.Println("map", reflect.ValueOf(map[string]int{}).Comparable())
	fmt.Println("func", reflect.ValueOf(main).Comparable())
	p := &Widget{1}
	fmt.Println("ptr", reflect.ValueOf(p).Comparable())

	// Value.Equal.
	fmt.Println("42==42", reflect.ValueOf(42).Equal(reflect.ValueOf(42)))
	fmt.Println("42==43", reflect.ValueOf(42).Equal(reflect.ValueOf(43)))
	fmt.Println("hi==hi", reflect.ValueOf("hi").Equal(reflect.ValueOf("hi")))
	fmt.Println("hi==ho", reflect.ValueOf("hi").Equal(reflect.ValueOf("ho")))
	fmt.Println("3.14==3.14", reflect.ValueOf(3.14).Equal(reflect.ValueOf(3.14)))
	fmt.Println("u8==u8", reflect.ValueOf(uint8(5)).Equal(reflect.ValueOf(uint8(5))))
	fmt.Println("int!=str", reflect.ValueOf(1).Equal(reflect.ValueOf("1")))
	fmt.Println("p==p", reflect.ValueOf(p).Equal(reflect.ValueOf(p)))
}
