package main

import (
	"errors"
	"fmt"
	"io/fs"
)

type timeoutErr struct{}

func (timeoutErr) Error() string { return "i/o timeout" }
func (timeoutErr) Timeout() bool { return true }

func main() {
	base := errors.New("permission denied")
	pe := &fs.PathError{Op: "open", Path: "/etc/secret", Err: base}
	fmt.Println("error:", pe.Error())
	fmt.Printf("op=%q path=%q\n", pe.Op, pe.Path)
	fmt.Println("unwrap is base:", errors.Is(pe, base))
	fmt.Println("unwrap:", errors.Unwrap(pe).Error())
	fmt.Println("timeout (plain):", pe.Timeout())

	te := &fs.PathError{Op: "read", Path: "/dev/x", Err: timeoutErr{}}
	fmt.Println("timeout (real):", te.Timeout())
	fmt.Println("te error:", te.Error())
}
