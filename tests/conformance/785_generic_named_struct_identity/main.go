package main

import "fmt"

// A generic function that does make([]T, n) and returns []*T pointing into the
// backing, instantiated with T = a package-level named struct. The named struct's
// identity must be preserved: goclr previously cloned it into a structural type
// (named by its fields) because substType allocated a fresh *Location instance for an
// unchanged pointer field, flagging the struct as "substituted". A later use as the
// named type then failed to cast. This is OPA's util.NewPtrSlice[ast.Term].
type Loc struct{ line int }
type Term struct {
	Value    interface{}
	Location *Loc
}

func newPtrSlice[T any](n int) []*T {
	p := make([]T, n)
	s := make([]*T, 0, n)
	for i := 0; i < n; i++ {
		s = append(s, &p[i])
	}
	return s
}

func main() {
	kvs := newPtrSlice[Term](2)
	kvs[0].Value = "key"
	kvs[1].Value = "val"
	fmt.Println("kvs:", kvs[0].Value, kvs[1].Value)

	// slice-to-array-pointer over the []*Term, then read the fields (the exact idiom).
	pair := *(*[2]*Term)(kvs[0:2])
	fmt.Println("pair:", pair[0].Value, pair[1].Value)
}
