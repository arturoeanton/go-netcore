package main

import (
	"errors"
	"fmt"
)

// errors.Is compares err == target for a comparable target: a value-receiver struct
// error matches by VALUE down the wrap/join chain, while pointer errors and errors.New
// sentinels keep REFERENCE equality.
type ValErr struct{ Code int }

func (e ValErr) Error() string { return fmt.Sprintf("E%d", e.Code) }

type PtrErr struct{ Code int }

func (e *PtrErr) Error() string { return fmt.Sprintf("P%d", e.Code) }

var ErrA = errors.New("a")
var ErrB = errors.New("b")

func main() {
	// sentinel: same identity matches, different does not
	w := fmt.Errorf("ctx: %w", ErrA)
	fmt.Println(errors.Is(w, ErrA), errors.Is(w, ErrB))

	// two distinct errors.New with the same message are NOT equal
	x := errors.New("same")
	y := errors.New("same")
	fmt.Println(errors.Is(x, y), errors.Is(x, x))

	// value error matches by value (through %w), distinct value does not
	v1 := ValErr{5}
	wv := fmt.Errorf("w: %w", v1)
	fmt.Println(errors.Is(wv, ValErr{5}), errors.Is(wv, ValErr{6}))
	fmt.Println(errors.Is(v1, ValErr{5}), errors.Is(v1, ValErr{6}))

	// pointer error keeps reference equality (same struct value, different pointer != )
	p1 := &PtrErr{1}
	p2 := &PtrErr{1}
	wp := fmt.Errorf("w: %w", p1)
	fmt.Println(errors.Is(wp, p1), errors.Is(wp, p2))

	// nil handling
	fmt.Println(errors.Is(nil, nil), errors.Is(nil, ErrA), errors.Is(ErrA, nil))

	// joined and multi-%w chains with value errors
	j := errors.Join(ErrA, v1)
	fmt.Println(errors.Is(j, ValErr{5}), errors.Is(j, ValErr{99}))
	m := fmt.Errorf("%w and %w", ErrA, v1)
	fmt.Println(errors.Is(m, ErrA), errors.Is(m, ValErr{5}))
}
