package main

import "fmt"

type List[T any] struct{ data []T }

func (l *List[T]) Add(x T)     { l.data = append(l.data, x) }
func (l *List[T]) Get(i int) T { return l.data[i] }

// A generic interface: dispatching a method through it must find the concrete
// instantiation (*List[int]) both when the interface is a plain parameter and when it
// is a parameter of a generic function (Container[T] with T substituted).
type Container[T any] interface {
	Add(T)
	Get(int) T
}

func FillInt(c Container[int], items []int) {
	for _, it := range items {
		c.Add(it)
	}
}

func Fill[T any](c Container[T], items []T) {
	for _, it := range items {
		c.Add(it)
	}
}

func main() {
	// concrete generic-interface parameter
	l := &List[int]{}
	FillInt(l, []int{10, 20, 30})
	fmt.Println(l.Get(0), l.Get(1), l.Get(2))

	// generic-function parameter of generic-interface type (Container[T])
	l2 := &List[int]{}
	Fill[int](l2, []int{1, 2, 3})
	fmt.Println(l2.Get(0), l2.Get(2))

	ls := &List[string]{}
	Fill[string](ls, []string{"a", "b", "c"})
	fmt.Println(ls.Get(0), ls.Get(2))

	// direct interface variable + dispatch
	var c Container[int] = &List[int]{}
	c.Add(7)
	c.Add(8)
	fmt.Println(c.Get(0), c.Get(1))
}
