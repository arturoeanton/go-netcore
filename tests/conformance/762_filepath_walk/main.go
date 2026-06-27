package main

import (
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"strings"
)

// filepath.Walk (fs.FileInfo) and filepath.WalkDir (fs.DirEntry): lexical-order tree walks with
// SkipDir / SkipAll handling. Walk was previously a dead stub (never invoked the callback) and
// WalkDir was unregistered. Directory entry order and the skip semantics match go run.
// (Directory FileInfo.Size() is filesystem-specific and not reproducible, so only file sizes
// are printed.)
func main() {
	dir, _ := os.MkdirTemp("", "gw-*")
	defer os.RemoveAll(dir)
	mk := func(p, c string) {
		full := filepath.Join(dir, p)
		os.MkdirAll(filepath.Dir(full), 0755)
		os.WriteFile(full, []byte(c), 0644)
	}
	mk("a.txt", "A")
	mk("b.txt", "B")
	mk("sub1/c.txt", "C")
	mk("sub1/d.txt", "D")
	mk("sub1/deep/e.txt", "E")
	mk("sub2/f.txt", "F")
	rel := func(p string) string { r, _ := filepath.Rel(dir, p); return r }

	fmt.Println("=== WalkDir all ===")
	filepath.WalkDir(dir, func(p string, d fs.DirEntry, err error) error {
		fmt.Printf("%s dir=%v\n", rel(p), d.IsDir())
		return nil
	})

	fmt.Println("=== WalkDir SkipDir sub1 ===")
	filepath.WalkDir(dir, func(p string, d fs.DirEntry, err error) error {
		if d.IsDir() && d.Name() == "sub1" {
			return fs.SkipDir
		}
		fmt.Println(rel(p))
		return nil
	})

	fmt.Println("=== WalkDir SkipAll after b.txt ===")
	filepath.WalkDir(dir, func(p string, d fs.DirEntry, err error) error {
		fmt.Println(rel(p))
		if strings.HasSuffix(p, "b.txt") {
			return fs.SkipAll
		}
		return nil
	})

	fmt.Println("=== Walk (FileInfo) ===")
	filepath.Walk(dir, func(p string, info os.FileInfo, err error) error {
		if info.IsDir() {
			fmt.Printf("%s dir=true\n", rel(p))
		} else {
			fmt.Printf("%s dir=false size=%d\n", rel(p), info.Size())
		}
		return nil
	})

	fmt.Println("=== Walk SkipDir from a file skips rest of its dir ===")
	filepath.Walk(dir, func(p string, info os.FileInfo, err error) error {
		if !info.IsDir() {
			fmt.Println(rel(p))
			if info.Name() == "c.txt" {
				return filepath.SkipDir
			}
		}
		return nil
	})
}
