package main

import (
	"errors"
	"fmt"
	"io/fs"
	"os"
)

// os file operations return a *os.PathError (== *fs.PathError) wrapping a syscall
// errno, so errors.As(*os.PathError)/(*fs.PathError), errors.Is(os.ErrNotExist /
// os.ErrExist / fs.ErrNotExist), and os.IsNotExist/IsExist all work. Previously these
// returned a plain error, so errors.As/Is failed and os.IsNotExist was inconsistent.
// (os.PathError is a Go type ALIAS of fs.PathError; errors.As must resolve the alias.)
func main() {
	// Open / ReadFile / Stat on a missing path.
	for _, probe := range []func(string) error{
		func(p string) error { _, e := os.Open(p); return e },
		func(p string) error { _, e := os.ReadFile(p); return e },
		func(p string) error { _, e := os.Stat(p); return e },
	} {
		err := probe("/no/such/file.txt")
		var pe *os.PathError
		var fe *fs.PathError
		fmt.Println(
			err != nil,
			errors.As(err, &pe), errors.As(err, &fe),
			errors.Is(err, os.ErrNotExist), errors.Is(err, fs.ErrNotExist),
			os.IsNotExist(err))
		fmt.Println(pe.Op, pe.Path)
	}

	// O_CREATE|O_EXCL on an existing file -> EEXIST.
	f, _ := os.CreateTemp("", "goclr-excl-*")
	name := f.Name()
	f.Close()
	_, e := os.OpenFile(name, os.O_CREATE|os.O_EXCL|os.O_WRONLY, 0644)
	var pe *os.PathError
	fmt.Println(e != nil, os.IsExist(e), errors.Is(e, os.ErrExist), errors.As(e, &pe), pe.Op)
	os.Remove(name)

	// Mkdir on an existing directory -> EEXIST.
	dir, _ := os.MkdirTemp("", "goclr-mk-*")
	e2 := os.Mkdir(dir, 0755)
	fmt.Println(os.IsExist(e2), errors.Is(e2, os.ErrExist))
	os.Remove(dir)

	// A missing file really is missing, an existing one is not.
	_, e3 := os.Stat(name)
	fmt.Println(os.IsNotExist(e3))
	d2, _ := os.MkdirTemp("", "goclr-x-*")
	_, e4 := os.Stat(d2)
	fmt.Println(e4 == nil, os.IsNotExist(e4))
	os.Remove(d2)
}
