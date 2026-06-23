package main

import (
	"errors"
	"fmt"
)

// errors.Is / errors.As / errors.Unwrap follow a user error type's own Unwrap()
// method (and consult an Is() method), not only the fmt.Errorf %w chain.
var ErrNotFound = errors.New("not found")

type QueryError struct {
	Query string
	Err   error
}

func (e *QueryError) Error() string { return e.Query + ": " + e.Err.Error() }
func (e *QueryError) Unwrap() error  { return e.Err }

type Sentinel struct{ Code int }

func (s *Sentinel) Error() string      { return fmt.Sprintf("code %d", s.Code) }
func (s *Sentinel) Is(t error) bool    { o, ok := t.(*Sentinel); return ok && o.Code == s.Code }

func main() {
	qe := &QueryError{"SELECT", ErrNotFound}
	fmt.Println(errors.Is(qe, ErrNotFound))

	wrapped := fmt.Errorf("layer: %w", qe)
	fmt.Println(errors.Is(wrapped, ErrNotFound))

	var target *QueryError
	fmt.Println(errors.As(wrapped, &target), target.Query)

	fmt.Println(errors.Unwrap(qe) == ErrNotFound)

	a, b := &Sentinel{42}, &Sentinel{42}
	fmt.Println(errors.Is(a, b))
	fmt.Println(errors.Is(a, &Sentinel{7}))
}
