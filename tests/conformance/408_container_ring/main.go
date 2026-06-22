package main

import (
	"container/ring"
	"fmt"
)

func main() {
	r := ring.New(5)
	for i := 0; i < r.Len(); i++ {
		r.Value = i + 1
		r = r.Next()
	}

	sum := 0
	r.Do(func(v any) { sum += v.(int) })
	fmt.Println("len:", r.Len(), "sum:", sum)
	fmt.Println("value:", r.Value, "next:", r.Next().Value, "prev:", r.Prev().Value)
	fmt.Println("move(2):", r.Move(2).Value)

	// Link two rings and report the combined length.
	a := ring.New(2)
	a.Value, a.Next().Value = "a", "b"
	b := ring.New(2)
	b.Value, b.Next().Value = "c", "d"
	a.Link(b)
	fmt.Println("linked len:", a.Len())
}
