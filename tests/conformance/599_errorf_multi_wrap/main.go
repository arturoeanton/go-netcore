package main

import (
	"errors"
	"fmt"
)

// fmt.Errorf with two or more %w verbs (Go 1.20) wraps every error: errors.Is/As
// search all of them, %T is *fmt.wrapErrors, and errors.Unwrap returns nil.
type NotFound struct{ Name string }

func (e *NotFound) Error() string { return e.Name + " not found" }

var (
	ErrA = errors.New("a")
	ErrB = errors.New("b")
	ErrC = errors.New("c")
)

func main() {
	m := fmt.Errorf("got %w, %w, %w", ErrA, ErrB, ErrC)
	fmt.Println(m)
	fmt.Println(errors.Is(m, ErrA), errors.Is(m, ErrB), errors.Is(m, ErrC))
	fmt.Printf("%T\n", m)
	fmt.Println(errors.Unwrap(m) == nil)

	// mixed %v and %w; extract a concrete type from the second wrap
	nf := &NotFound{Name: "user"}
	m2 := fmt.Errorf("code=%v: %w and %w", 404, ErrA, nf)
	fmt.Println(m2)
	var target *NotFound
	fmt.Println(errors.As(m2, &target), target.Name)
	fmt.Println(errors.Is(m2, ErrA))

	// single %w stays *fmt.wrapError, no %w stays *errors.errorString
	fmt.Printf("%T\n", fmt.Errorf("one %w", ErrA))
	fmt.Printf("%T\n", fmt.Errorf("none %v", 1))

	// nested: a multi-wrap inside a single wrap is still reachable
	inner := fmt.Errorf("inner %w + %w", ErrA, ErrB)
	outer := fmt.Errorf("outer: %w", inner)
	fmt.Println(errors.Is(outer, ErrA), errors.Is(outer, ErrB), errors.Is(outer, ErrC))
}
