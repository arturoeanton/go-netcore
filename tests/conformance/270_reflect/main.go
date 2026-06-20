package main

import "reflect"

type Point struct{ X, Y int }

func main() {
	x := 42
	println(reflect.TypeOf(x).Kind() == reflect.Int)
	println(reflect.TypeOf("hi").Kind() == reflect.String)
	println(reflect.ValueOf(x).Int())
	println(reflect.ValueOf("hi").String())
	sl := []int{10, 20, 30}
	v := reflect.ValueOf(sl)
	println(v.Kind() == reflect.Slice, v.Len(), v.Index(1).Int())
	p := Point{X: 3, Y: 7}
	pv := reflect.ValueOf(p)
	println(pv.Kind() == reflect.Struct, pv.NumField(), pv.Field(0).Int(), pv.Field(1).Int())
	println(reflect.DeepEqual([]int{1, 2}, []int{1, 2}))
	println(reflect.DeepEqual([]int{1, 2}, []int{1, 3}))
	m := map[string]int{"a": 1}
	mv := reflect.ValueOf(m)
	println(mv.Kind() == reflect.Map, mv.Len())
}
