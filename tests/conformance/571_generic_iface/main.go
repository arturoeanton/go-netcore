package main

import (
	"fmt"
	"sort"
)

// A generic type whose methods satisfy interfaces (fmt.Stringer / error), used both
// via explicit interface dispatch and implicitly by fmt.
type Pair[K comparable, V any] struct {
	Key K
	Val V
}

func (p Pair[K, V]) String() string { return fmt.Sprintf("%v=%v", p.Key, p.Val) }

type WrapErr[T any] struct{ v T }

func (w WrapErr[T]) Error() string { return fmt.Sprintf("wrapped(%v)", w.v) }

// A generic type implementing a user interface dispatched in a slice.
type Named interface{ Name() string }

type Tagged[T any] struct {
	tag string
	val T
}

func (t Tagged[T]) Name() string { return t.tag }

func main() {
	// Explicit Stringer dispatch.
	var s fmt.Stringer = Pair[string, int]{"age", 30}
	fmt.Println(s.String())

	// Implicit via fmt for two distinct instantiations.
	fmt.Println(Pair[string, int]{"x", 1})
	fmt.Println(Pair[int, string]{2, "y"})

	// Generic type as error.
	var e error = WrapErr[int]{42}
	fmt.Println(e.Error())
	fmt.Println(e)

	// Slice of a user interface holding generic instantiations.
	items := []Named{
		Tagged[int]{"first", 1},
		Tagged[string]{"second", "two"},
	}
	names := []string{}
	for _, it := range items {
		names = append(names, it.Name())
	}
	sort.Strings(names)
	fmt.Println(names)
}
