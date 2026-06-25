package main

import (
	"fmt"
	"iter"
	"maps"
	"slices"
)

// A local struct type inside a generic function whose fields are the type parameters.
// Each instantiation must get its own monomorphized layout.
func roundtrip[K any, V any](k K, v V) (K, V) {
	type pair struct {
		k K
		v V
	}
	p := pair{k, v}
	return p.k, p.v
}

// Reached through a channel (the iter.Pull2 shape).
func viaChan[T any](xs []T) []T {
	type box struct{ v T }
	ch := make(chan box, len(xs))
	for _, x := range xs {
		ch <- box{x}
	}
	close(ch)
	out := []T{}
	for b := range ch {
		out = append(out, b.v)
	}
	return out
}

// Reached through a slice and a pointer.
func viaSlicePtr[T any](xs []T) []T {
	type wrap struct {
		val T
		idx int
	}
	ws := []*wrap{}
	for i, x := range xs {
		ws = append(ws, &wrap{val: x, idx: i})
	}
	out := []T{}
	for _, w := range ws {
		out = append(out, w.val)
	}
	return out
}

func main() {
	a, b := roundtrip("hello", 42)
	fmt.Println(a, b)
	c, d := roundtrip(3.14, "world")
	fmt.Println(c, d)

	fmt.Println(viaChan([]string{"x", "y", "z"}))
	fmt.Println(viaChan([]int{1, 2, 3}))
	fmt.Println(viaSlicePtr([]int{7, 8, 9}))
	fmt.Println(viaSlicePtr([]string{"p", "q"}))

	// iter.Pull / Pull2 (implemented via a goroutine + a generic-struct channel).
	next, stop := iter.Pull(slices.Values([]int{10, 20, 30}))
	defer stop()
	sum := 0
	for {
		v, ok := next()
		if !ok {
			break
		}
		sum += v
	}
	fmt.Println("pull-sum", sum)

	n2, s2 := iter.Pull2(maps.All(map[string]int{"only": 5}))
	defer s2()
	mk, mv, ok := n2()
	fmt.Println(mk, mv, ok)
}
