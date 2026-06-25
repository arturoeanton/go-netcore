package main

import (
	"fmt"
	"reflect"
)

type Pair[K comparable, V any] struct {
	Key K
	Val V
}

type List[T any] struct{ items []T }

func main() {
	// %T of a generic instantiation matches Go's reflect form (no space after comma).
	fmt.Printf("%T\n", Pair[string, int]{"a", 1})
	fmt.Printf("%T\n", Pair[int, string]{1, "a"})
	fmt.Printf("%T\n", List[float64]{})

	// Through composites.
	fmt.Printf("%T\n", []Pair[string, int]{})
	fmt.Printf("%T\n", map[string]Pair[int, string]{})
	fmt.Printf("%T\n", &Pair[string, int]{})
	fmt.Printf("%T\n", [][]List[int]{})

	// reflect.Type.String agrees.
	fmt.Println(reflect.TypeOf(Pair[string, int]{}).String())
	fmt.Println(reflect.TypeOf(List[bool]{}).String())

	// Non-generic types are unaffected.
	type User struct{ Name string }
	fmt.Printf("%T %T\n", User{}, []User{})
}
