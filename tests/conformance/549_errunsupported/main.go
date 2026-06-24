package main

import (
	"errors"
	"fmt"
)

func main() {
	wrapped := fmt.Errorf("readlink: %w", errors.ErrUnsupported)
	fmt.Println("sentinel:", errors.ErrUnsupported)
	fmt.Println("wrapped:", wrapped)
	fmt.Println("is unsupported:", errors.Is(wrapped, errors.ErrUnsupported))
	fmt.Println("is other:", errors.Is(wrapped, errors.New("x")))
}
