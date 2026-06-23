package main

import "fmt"

// Composite types keep their precise element/key types through an interface, so %T,
// %#v, and a type switch report []int / map[string]int / main.IntSlice rather than
// the erased []interface {}.
type IntSlice []int
type StrMap map[string]int

func describe(v interface{}) string {
	switch v.(type) {
	case []int:
		return "[]int"
	case []string:
		return "[]string"
	case map[string]int:
		return "map[string]int"
	case IntSlice:
		return "IntSlice"
	default:
		return "other"
	}
}

func main() {
	fmt.Printf("%T %T %T\n", []int{1, 2}, map[string]int{"a": 1}, []string{"x"})
	fmt.Printf("%T %T\n", IntSlice{1}, StrMap{"k": 2})
	fmt.Printf("%T %T\n", [][]int{{1}}, map[int][]string{})
	fmt.Printf("%T %T\n", []interface{}{1}, map[string]interface{}{})

	fmt.Printf("%#v\n", []int{1, 2, 3})
	fmt.Printf("%#v\n", map[string]int{"a": 1})
	fmt.Printf("%#v\n", IntSlice{4, 5})
	fmt.Printf("%#v\n", []string{"x", "y"})

	fmt.Println(describe([]int{1}), describe([]string{"a"}), describe(map[string]int{}), describe(IntSlice{1}), describe(42))

	// %T after passing through an any-typed function parameter.
	report := func(v interface{}) { fmt.Printf("%T\n", v) }
	report([]float64{1.5})
	report(map[int]bool{1: true})
}
