package main

import "fmt"

// %#v spells nested composite element/key/value types, not the erased
// []interface {}, and renders non-string map keys without quotes.
func main() {
	fmt.Printf("%#v\n", [][]int{{1, 2}, {3}})
	fmt.Printf("%#v\n", map[int][]string{1: {"a", "b"}})
	fmt.Printf("%#v\n", map[string]int{"x": 1, "y": 2})
	fmt.Printf("%#v\n", map[int]int{1: 10, 2: 20})
	fmt.Printf("%#v\n", []map[string]int{{"a": 1}})
	fmt.Printf("%#v\n", []int{1, 2})
	fmt.Printf("%#v\n", []string{"p", "q"})
}
