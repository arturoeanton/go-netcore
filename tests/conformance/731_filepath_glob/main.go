package main

import (
	"fmt"
	"os"
	"path/filepath"
)

// filepath.Glob returns the names of files matching a Match-syntax pattern, sorted
// within each directory; file-system errors are ignored (missing dir -> no matches),
// only a malformed pattern returns ErrBadPattern, and no matches yields a nil slice.
func main() {
	dir, err := os.MkdirTemp("", "glob-fixture-*")
	if err != nil {
		fmt.Println("mkdir:", err)
		return
	}
	defer os.RemoveAll(dir)
	os.MkdirAll(filepath.Join(dir, "sub"), 0755)
	for _, n := range []string{"a.txt", "b.txt", "c.go", "ab.txt", "1.log", "sub/x.txt", "sub/y.go"} {
		os.WriteFile(filepath.Join(dir, n), []byte("x"), 0644)
	}

	show := func(pat string) {
		m, err := filepath.Glob(filepath.Join(dir, pat))
		var names []string
		for _, p := range m {
			names = append(names, filepath.Base(p))
		}
		fmt.Printf("%-10s -> %v (nil=%v) err=%v\n", pat, names, m == nil, err)
	}
	show("*.txt")
	show("*.go")
	show("?.txt")
	show("[ab]*.txt")
	show("*")
	show("nomatch*")
	show("a.txt")
	show("sub/*.txt")
	show("*/*.go")
	show("*.xyz")

	// Non-meta, non-existent path -> nil, nil.
	m, e := filepath.Glob(filepath.Join(dir, "does-not-exist.txt"))
	fmt.Println(m == nil, e)

	// Malformed pattern -> ErrBadPattern.
	_, e2 := filepath.Glob("[")
	fmt.Println(e2)
}
