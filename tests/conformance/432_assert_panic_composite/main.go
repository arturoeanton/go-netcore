package main

import "fmt"

// A failed type assertion to a composite or named type panics with Go's exact
// "interface conversion: <iface> is <actual>, not <asserted>" message — the same as
// for a basic type — not a generic "does not implement" message.
type IntSlice []int

func try(name string, f func()) {
	defer func() {
		if r := recover(); r != nil {
			fmt.Printf("%s: %v\n", name, r)
		}
	}()
	f()
}

func main() {
	try("slice->map", func() { var x interface{} = []int{1}; _ = x.(map[string]int) })
	try("int->slice", func() { var x interface{} = 5; _ = x.([]int) })
	try("str->namedslice", func() { var x interface{} = "s"; _ = x.(IntSlice) })
	try("map->slice", func() { var x interface{} = map[string]int{"a": 1}; _ = x.([]string) })

	// successful composite assertions still work
	var a interface{} = []int{1, 2, 3}
	fmt.Println(a.([]int))
	var b interface{} = map[string]int{"k": 9}
	fmt.Println(b.(map[string]int))
	var c interface{} = IntSlice{7, 8}
	fmt.Println(c.(IntSlice))
}
