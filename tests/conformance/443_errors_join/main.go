package main

import (
	"errors"
	"fmt"
)

type notFound struct{ key string }

func (e *notFound) Error() string { return "not found: " + e.key }

var errSentinel = errors.New("sentinel")

func main() {
	// Error() reports the joined messages newline-separated.
	fmt.Println(errors.Join(errors.New("first"), errors.New("second")))

	// nil arguments are dropped; an all-nil (or empty) join is nil.
	fmt.Println(errors.Join(nil, nil) == nil)

	// errors.Is descends into every joined error.
	j := errors.Join(nil, errSentinel, nil)
	fmt.Println(errors.Is(j, errSentinel))

	// errors.As finds a joined custom error.
	nf := &notFound{"k"}
	j2 := errors.Join(errors.New("other"), nf)
	var target *notFound
	fmt.Println(errors.As(j2, &target), target.key)

	// joins compose with %w wrapping.
	w := fmt.Errorf("ctx: %w", errSentinel)
	j3 := errors.Join(w, errors.New("z"))
	fmt.Println(errors.Is(j3, errSentinel))
	fmt.Println(j3)
}
