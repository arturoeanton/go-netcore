package main

import (
	"fmt"
	"reflect"
)

type Addr struct{ City string }
type Person struct {
	Name string
	Age  int
	Tags []string
	Addr Addr
}

// reflect deep-dive incl. IsNil across kinds: a nil pointer/slice/map/func/chan is nil; a
// non-nil pointer and an empty-but-non-nil slice/map are not. Plus field iteration, Set
// through a pointer, New/Interface, and DeepEqual.
func main() {
	p := Person{"Bob", 30, []string{"a", "b"}, Addr{"NYC"}}
	v := reflect.ValueOf(p)
	t := v.Type()
	for i := 0; i < t.NumField(); i++ {
		f := t.Field(i)
		fmt.Printf("%s: %v (%s, kind=%s)\n", f.Name, v.Field(i).Interface(), f.Type, v.Field(i).Kind())
	}

	x := Person{}
	rv := reflect.ValueOf(&x).Elem()
	rv.FieldByName("Name").SetString("set")
	rv.FieldByName("Age").SetInt(99)
	fmt.Println(x.Name, x.Age)

	nt := reflect.New(reflect.TypeOf(Person{}))
	nt.Elem().FieldByName("Name").SetString("new")
	fmt.Println(nt.Elem().Interface().(Person).Name)

	fmt.Println(reflect.DeepEqual(p, Person{"Bob", 30, []string{"a", "b"}, Addr{"NYC"}}))

	var pi *int
	var pt *Person
	var s []int
	var m map[string]int
	var fn func()
	var ch chan int
	fmt.Println(reflect.ValueOf(pi).IsNil(), reflect.ValueOf(pt).IsNil())
	fmt.Println(reflect.ValueOf(s).IsNil(), reflect.ValueOf(m).IsNil())
	fmt.Println(reflect.ValueOf(fn).IsNil(), reflect.ValueOf(ch).IsNil())

	n := 5
	pp := &n
	fmt.Println(reflect.ValueOf(pp).IsNil(), reflect.ValueOf(&Person{}).IsNil())
	fmt.Println(reflect.ValueOf([]int{}).IsNil(), reflect.ValueOf(map[string]int{}).IsNil())
}
