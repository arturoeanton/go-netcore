package main

import "fmt"

// Interface dispatch on a GENERIC type instantiation (*pair[bool]) that is created
// only inside another generic function (New[bool]) and never referenced by name at
// the call site. Its monomorphization happens after the body pass that generates the
// isinst switches, so the dispatch relies on the no-match fallback bridge covering
// generic instantiations. This is the lestrrat-go/option pattern jwx (and thus OPA)
// uses; before the fix it paniced with a bogus nil-pointer dereference.
type Option interface {
	Ident() any
	Value(dst any) error
}

type pair[T any] struct {
	ident any
	value T
}

func New[T any](ident any, value T) Option {
	return &pair[T]{ident: ident, value: value}
}

func (p *pair[T]) Ident() any { return p.ident }
func (p *pair[T]) Value(dst any) error {
	*(dst.(*T)) = p.value
	return nil
}

type identName struct{}
type identCount struct{}

func main() {
	a := New[bool](identName{}, true)
	b := New[int](identCount{}, 42)

	fmt.Printf("a ident: %T\n", a.Ident())
	fmt.Printf("b ident: %T\n", b.Ident())

	var bv bool
	_ = a.Value(&bv)
	var iv int
	_ = b.Value(&iv)
	fmt.Println("values:", bv, iv)
}
