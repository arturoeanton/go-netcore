package main

import (
	"errors"
	"fmt"
	"hash/maphash"
)

type MyErr struct{ Code int }

func (e *MyErr) Error() string { return fmt.Sprintf("myerr %d", e.Code) }

var ErrSentinel = errors.New("sentinel")

// errors wrapping/Is/As/Join, plus hash/maphash. maphash uses a random seed in Go,
// so absolute Sum64 values are not byte-stable — only the relative properties
// (same seed+bytes -> same hash, SetSeed/Seed round-trip, incremental == one-shot)
// are observable, and those must match go run exactly.
func main() {
	// --- errors ---
	wrapped := fmt.Errorf("layer1: %w", fmt.Errorf("layer2: %w", ErrSentinel))
	fmt.Println(wrapped)
	fmt.Println(errors.Is(wrapped, ErrSentinel))
	fmt.Println(errors.Unwrap(errors.Unwrap(wrapped)) == ErrSentinel)

	var me *MyErr
	e2 := fmt.Errorf("wrap: %w", &MyErr{Code: 42})
	fmt.Println(errors.As(e2, &me), me.Code)

	joined := errors.Join(ErrSentinel, &MyErr{Code: 1}, errors.New("third"))
	fmt.Println(joined)
	fmt.Println(errors.Is(joined, ErrSentinel))
	var me2 *MyErr
	fmt.Println(errors.As(joined, &me2), me2.Code)
	fmt.Println(errors.Is(errors.New("x"), ErrSentinel))

	// --- maphash (relative properties only) ---
	seed := maphash.MakeSeed()
	fmt.Println(maphash.String(seed, "hello") == maphash.Bytes(seed, []byte("hello")))

	var h maphash.Hash
	h.SetSeed(seed)
	h.WriteString("hello")
	fmt.Println(h.Sum64() == maphash.String(seed, "hello"))
	fmt.Println(h.Seed() == seed)

	first := h.Sum64()
	h.Reset()
	h.WriteString("hello")
	fmt.Println(h.Sum64() == first)

	h.Reset()
	h.WriteString("foo")
	h.WriteString("bar")
	fmt.Println(h.Sum64() == maphash.String(seed, "foobar"))

	h.Reset()
	h.WriteByte('x')
	h.WriteByte('y')
	fmt.Println(h.Sum64() == maphash.String(seed, "xy"))

	h.Reset()
	h.Write([]byte("data"))
	fmt.Println(h.Sum64() == maphash.Bytes(seed, []byte("data")))

	fmt.Println(maphash.String(seed, "a") != maphash.String(seed, "b"))
	fmt.Println(h.Size(), h.BlockSize())

	// Two distinct seeds almost never collide on the same input.
	s2 := maphash.MakeSeed()
	fmt.Println(maphash.String(seed, "k") != maphash.String(s2, "k") || seed == s2)
}
