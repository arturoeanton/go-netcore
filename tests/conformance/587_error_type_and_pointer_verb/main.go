package main

import (
	"errors"
	"fmt"
)

type MyErr struct{ Code int }

func (e *MyErr) Error() string { return fmt.Sprintf("code %d", e.Code) }

// %T must distinguish the standard error implementations, and %p is a bad verb
// for any non-pointer-like kind (Go prints %!p(type=value)).
func main() {
	base := &MyErr{Code: 42}
	wrapped := fmt.Errorf("layer1: %w", base) // *fmt.wrapError
	plain := fmt.Errorf("no wrap here")        // *errors.errorString
	enew := errors.New("simple")               // *errors.errorString
	joined := errors.Join(errors.New("a"), errors.New("b")) // *errors.joinError

	fmt.Printf("%T\n", base)
	fmt.Printf("%T\n", wrapped)
	fmt.Printf("%T\n", plain)
	fmt.Printf("%T\n", enew)
	fmt.Printf("%T\n", joined)

	// errors.Is/As/Unwrap still work through the wrap chain.
	var t *MyErr
	fmt.Println(errors.As(wrapped, &t), t.Code)
	fmt.Println(errors.Unwrap(wrapped))

	// %p is a bad verb for non-pointer kinds.
	fmt.Printf("%p\n", true)
	fmt.Printf("%p\n", 42)
	fmt.Printf("%p\n", "str")
	fmt.Printf("%p\n", 3.14)

	// %p on pointer-like kinds yields an 0x address (just check the prefix; the
	// address itself is non-deterministic in both runtimes).
	s := []int{1, 2, 3}
	m := map[string]int{"x": 1}
	fmt.Println(fmt.Sprintf("%p", &base)[:2], fmt.Sprintf("%p", s)[:2], fmt.Sprintf("%p", m)[:2])
}
