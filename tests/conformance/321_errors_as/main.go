package main

import (
	"errors"
	"fmt"
)

type NotFoundError struct{ Key string }

func (e *NotFoundError) Error() string { return "not found: " + e.Key }

type ValidationError struct{ Field string }

func (e *ValidationError) Error() string { return "invalid: " + e.Field }

func find(k string) error {
	return fmt.Errorf("lookup failed: %w", &NotFoundError{Key: k})
}

func main() {
	err := find("user42")

	var nf *NotFoundError
	if errors.As(err, &nf) {
		fmt.Println("not-found key:", nf.Key)
	}

	var ve *ValidationError
	fmt.Println("is validation:", errors.As(err, &ve))

	// errors.Is still works alongside.
	sentinel := errors.New("sentinel")
	wrapped := fmt.Errorf("ctx: %w", sentinel)
	fmt.Println("is sentinel:", errors.Is(wrapped, sentinel))

	// unwrap chain
	fmt.Println(errors.Unwrap(err).Error())
}
