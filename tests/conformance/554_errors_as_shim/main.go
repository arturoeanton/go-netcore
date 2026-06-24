package main

import (
	"errors"
	"fmt"
	"io/fs"
	"os"
)

func main() {
	// errors.As into a shim error struct (*fs.PathError) — through a wrapping chain.
	base := errors.New("permission denied")
	pe := &fs.PathError{Op: "open", Path: "/etc/x", Err: base}
	wrapped := fmt.Errorf("config load: %w", pe)

	var target *fs.PathError
	fmt.Println("as path:", errors.As(wrapped, &target))
	if target != nil {
		fmt.Printf("  op=%q path=%q\n", target.Op, target.Path)
	}

	// errors.As into *os.LinkError.
	le := &os.LinkError{Op: "symlink", Old: "/a", New: "/b", Err: base}
	var lt *os.LinkError
	fmt.Println("as link:", errors.As(le, &lt), lt.New)

	// Mismatched shim type must not match.
	var wrong *os.LinkError
	fmt.Println("as wrong:", errors.As(pe, &wrong))

	// And errors.Is still works alongside As.
	fmt.Println("is base:", errors.Is(wrapped, base))
}
